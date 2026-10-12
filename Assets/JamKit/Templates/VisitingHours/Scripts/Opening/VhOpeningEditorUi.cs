using System.Collections.Generic;
using System.Globalization;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening editor's panels (docs/OPENING-EDITOR.md): the list of things on the left (with what you've changed),
    /// the selected thing on the right (numbers you can type or scrub), the timeline along the top, the tools, the mode
    /// strip, the status line and the keys along the bottom, the help (H) and the "reset everything" question. Every
    /// control is a plain element hit-tested here from the mouse's position (see VhOpeningEditor.cs), so a click on a
    /// panel never reaches the view, and nothing silently waits for an event the shell doesn't send.
    /// </summary>
    public sealed partial class VhOpeningEditor
    {
        sealed class Ctl
        {
            public string id = "", tip = "";
            public VisualElement el;
            public Label label;
            public System.Action click, dbl;
            /// <summary>Also has to be inside this to be hit (the list's scrolled viewport).</summary>
            public VisualElement clip;
            public NumberBox number;
            public bool track, scrollbar, on, hovered, row;
        }

        sealed class NumberBox
        {
            public string id = "";
            /// <summary>0 position (cm), 1 turn (degrees), 2 size (%), 3 field of view (degrees).</summary>
            public int group, axis;
            public VisualElement el;
            public Label value;
            public Ctl ctl;
        }

        static readonly Color Ink = new Color(0.94f, 0.94f, 0.96f);
        static readonly Color Dim = new Color(0.68f, 0.71f, 0.78f);
        static readonly Color Accent = new Color(1f, 0.79f, 0.29f);
        static readonly Color SelColor = new Color(1f, 0.6f, 0.12f);
        static readonly Color PanelBg = new Color(0.055f, 0.06f, 0.075f, 1f);
        static readonly Color ButtonBg = new Color(0.17f, 0.19f, 0.235f);
        static readonly Color ButtonHover = new Color(0.27f, 0.3f, 0.38f);
        static readonly Color ButtonOn = new Color(0.26f, 0.42f, 0.66f);
        static readonly Color RowBg = new Color(0f, 0f, 0f, 0f);
        static readonly Color FieldBg = new Color(0.12f, 0.13f, 0.165f);
        static readonly Color[] AxisColors = { new Color(1f, 0.36f, 0.36f), new Color(0.45f, 0.92f, 0.4f), new Color(0.4f, 0.62f, 1f) };

        const float LeftWidth = 290f, RightWidth = 360f, Gap = 8f, BottomSpace = 132f;

        VisualElement _root, _view, _tagLayer, _left, _right, _top, _bottom, _help, _confirm, _changes, _track, _beatRow, _selBox, _scaleBlock, _shotBox, _statusBox;
        ScrollView _list;
        Label _status, _tip, _modeLabel, _timeLabel, _beatLabel, _selName, _selWhere, _selNote, _noSel, _confirmText, _changesTitle, _scaleNote, _selTag, _hoverTag, _offTag;
        Ctl _playBtn, _toolMove, _toolTurn, _toolSize, _snapBtn, _camBtn, _lightBtn, _saveBtn, _spaceBtn, _leftTab, _rightTab, _hoverCtl, _pressCtl, _lastClickCtl;
        float _lastClickTime = -10f;
        readonly List<Ctl> _ctls = new List<Ctl>();
        readonly List<Ctl> _listCtls = new List<Ctl>(), _beatCtls = new List<Ctl>(), _changeCtls = new List<Ctl>();
        readonly Dictionary<VhEditable, Ctl> _rowOf = new Dictionary<VhEditable, Ctl>();
        readonly HashSet<string> _changedIds = new HashSet<string>();
        NumberBox[] _pos, _rot, _scl;
        NumberBox _fovBox, _editing;
        string _editText = "";
        bool _editReplace, _numberScrub, _helpShown, _confirmShown, _leftHidden, _rightHidden;
        float _numberStart;
        string _statusText = "";
        float _statusAge = 99f, _shownTime = -1f, _hoverSince, _shownBottom = -1f;
        int _shownBeat = -2;

        public bool HelpShown => _helpShown;
        public bool ConfirmShown => _confirmShown;
        /// <summary>A number box is taking typing (shortcuts are off meanwhile).</summary>
        public bool Typing => _editing != null;

        // ---------- Building ----------

        static VisualElement Box(VisualElement parent, string name = null)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            parent?.Add(e);
            return e;
        }

        static Label Text(VisualElement parent, string text, float size, Color color, bool bold = false)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = color;
            l.style.fontSize = size;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingLeft = l.style.paddingRight = 0;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent?.Add(l);
            return l;
        }

        static void Panel(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.backgroundColor = PanelBg;
            e.style.paddingLeft = e.style.paddingRight = 10;
            e.style.paddingTop = e.style.paddingBottom = 8;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 6;
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = 1;
            var edge = new Color(1f, 1f, 1f, 0.09f);
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = edge;
        }

        static VisualElement Row(VisualElement parent, float marginTop = 0f)
        {
            var r = Box(parent);
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.flexWrap = Wrap.Wrap;
            r.style.marginTop = marginTop;
            return r;
        }

        Ctl Button(VisualElement parent, string id, string text, string tip, System.Action click, float size = 14f)
        {
            var el = Box(parent, id);
            el.style.backgroundColor = ButtonBg;
            el.style.paddingLeft = el.style.paddingRight = 9;
            el.style.paddingTop = el.style.paddingBottom = 4;
            el.style.marginRight = 4;
            el.style.marginTop = el.style.marginBottom = 2;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 4;
            el.style.justifyContent = Justify.Center;
            var l = Text(el, text, size, Ink);
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            var c = new Ctl { id = id, el = el, label = l, click = click, tip = tip ?? "" };
            _ctls.Add(c);
            return c;
        }

        Ctl ListRow(VisualElement parent, string id, string text, string tip, System.Action click, System.Action dbl, VisualElement clip)
        {
            var c = Button(parent, id, text, tip, click, 14.5f);
            c.row = true;
            c.dbl = dbl;
            c.clip = clip;
            c.el.style.backgroundColor = RowBg;
            c.el.style.marginRight = 0;
            c.el.style.marginTop = c.el.style.marginBottom = 0;
            c.el.style.paddingLeft = 6;
            c.label.style.unityTextAlign = TextAnchor.MiddleLeft;
            c.label.style.whiteSpace = WhiteSpace.Normal;
            return c;
        }

        NumberBox[] NumberRow(VisualElement parent, string title, int group, string idBase)
        {
            Text(parent, title, 14f, Dim).style.marginTop = 6;
            var row = Row(parent);
            row.style.flexWrap = Wrap.NoWrap;
            var boxes = new NumberBox[3];
            string[] axes = { "x", "y", "z" };
            for (int i = 0; i < 3; i++) boxes[i] = Number(row, idBase + "." + axes[i], axes[i].ToUpperInvariant(), group, i, AxisColors[i]);
            return boxes;
        }

        NumberBox Number(VisualElement parent, string id, string axisLabel, int group, int axis, Color axisColor)
        {
            var el = Box(parent, id);
            el.style.flexDirection = FlexDirection.Row;
            el.style.alignItems = Align.Center;
            el.style.flexGrow = 1;
            el.style.flexBasis = 0;
            el.style.height = 30;
            el.style.marginRight = 4;
            el.style.paddingLeft = 6;
            el.style.paddingRight = 4;
            el.style.backgroundColor = FieldBg;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 4;
            el.style.borderLeftWidth = el.style.borderRightWidth = el.style.borderTopWidth = el.style.borderBottomWidth = 1;
            el.style.borderLeftColor = el.style.borderRightColor = el.style.borderTopColor = el.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            var a = Text(el, axisLabel, 13f, axisColor, true);
            a.style.width = 14;
            a.style.whiteSpace = WhiteSpace.NoWrap;
            var v = Text(el, "", 15f, Ink);
            v.style.whiteSpace = WhiteSpace.NoWrap;
            v.style.flexGrow = 1;
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            var b = new NumberBox { id = id, group = group, axis = axis, el = el, value = v };
            var c = new Ctl
            {
                id = "field:" + id, el = el, label = v, number = b,
                tip = "Click to type a number (Enter applies, Tab the next box, Esc cancels). Drag left or right to scrub it (Shift: finer).",
            };
            b.ctl = c;
            _ctls.Add(c);
            return b;
        }

        void BuildUi()
        {
            RemoveUi();
            _root = new VisualElement { name = "vh-opening-editor", pickingMode = PickingMode.Position };
            _root.style.position = Position.Absolute;
            _root.style.left = _root.style.right = _root.style.top = _root.style.bottom = 0;
            _root.style.fontSize = 15;
            _root.style.color = Ink;

            // The view: outlines and handles are drawn on it.
            _view = Box(_root, "view");
            _view.style.position = Position.Absolute;
            _view.style.left = _view.style.right = _view.style.top = _view.style.bottom = 0;
            _view.generateVisualContent += Draw;
            _tagLayer = Box(_root, "tags");
            _tagLayer.style.position = Position.Absolute;
            _tagLayer.style.left = _tagLayer.style.right = _tagLayer.style.top = _tagLayer.style.bottom = 0;
            _selTag = Tag(SelColor);
            _hoverTag = Tag(Color.white);
            _offTag = Tag(SelColor);

            BuildLeft();
            BuildRight();
            BuildTimeline();
            BuildBottom();
            BuildHelp();
            BuildConfirm();
            ApplyPanelPlaces();

            var title = GameRoot.Ui.Title;
            var layer = title != null && title.Root != null ? title.Root.parent : null;
            if (layer != null) layer.Add(_root);
            else GameRoot.Ui.Root.Add(_root);
            SetTool(_tool, false);
        }

        Label Tag(Color color)
        {
            var l = Text(_tagLayer, "", 14f, color, true);
            l.style.position = Position.Absolute;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
            l.style.paddingLeft = l.style.paddingRight = 6;
            l.style.paddingTop = l.style.paddingBottom = 2;
            l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 3;
            l.style.display = DisplayStyle.None;
            return l;
        }

        void BuildLeft()
        {
            _left = Box(_root, "things");
            Panel(_left);
            _left.style.left = Gap;
            _left.style.top = Gap;
            _left.style.bottom = BottomSpace;
            _left.style.width = LeftWidth;
            var head = Row(_left);
            head.style.flexWrap = Wrap.NoWrap;
            var t = Text(head, "Opening editor", 19f, Accent, true);
            t.style.flexGrow = 1;
            Button(head, "hide:left", "Hide", "Hides this list (the button at the top left brings it back).", () => SetLeftHidden(true), 13f);

            _changesTitle = Text(_left, "What you've changed", 15f, Ink, true);
            _changesTitle.style.marginTop = 8;
            _changes = Box(_left, "changes");
            _changes.style.marginBottom = 6;

            var all = Text(_left, "Everything you can move", 15f, Ink, true);
            all.style.marginTop = 4;
            Text(_left, "Click: select it.  Double-click: select it and frame it.", 13f, Dim);
            _list = new ScrollView(ScrollViewMode.Vertical) { name = "list" };
            _list.style.flexGrow = 1;
            _list.style.marginTop = 4;
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _left.Add(_list);
            BuildList();
            // Its scrollbar: drag it (the wheel scrolls the list too).
            _ctls.Add(new Ctl { id = "scrollbar", el = _list.verticalScroller, scrollbar = true, tip = "Drag to scroll the list (or use the wheel over it)." });

            _leftTab = Button(_root, "show:left", "Things >", "Shows the list of everything you can move.", () => SetLeftHidden(false), 14f);
            _leftTab.el.style.position = Position.Absolute;
            _leftTab.el.style.left = Gap;
            _leftTab.el.style.top = Gap;
            _leftTab.el.style.display = DisplayStyle.None;
        }

        void BuildRight()
        {
            _right = Box(_root, "selected");
            Panel(_right);
            _right.style.right = Gap;
            _right.style.top = Gap;
            _right.style.width = RightWidth;
            var head = Row(_right);
            head.style.flexWrap = Wrap.NoWrap;
            head.style.alignItems = Align.FlexStart;
            _selName = Text(head, "Nothing selected", 18f, SelColor, true);
            _selName.style.flexGrow = 1;
            _selName.style.flexShrink = 1;
            Button(head, "hide:right", "Hide", "Hides this panel (the button at the top right brings it back).", () => SetRightHidden(true), 13f);
            _noSel = Text(_right, "Click a thing in the view, or pick it from the list on the left. Its numbers, its notes and its buttons show here.", 14f, Dim);
            _noSel.style.marginTop = 4;

            _selBox = Box(_right, "selection");
            _selWhere = Text(_selBox, "", 13f, Dim);
            _selWhere.style.marginTop = 2;
            _selNote = Text(_selBox, "", 13.5f, Ink);
            _selNote.style.marginTop = 6;
            _selNote.style.paddingLeft = 8;
            _selNote.style.paddingTop = _selNote.style.paddingBottom = 4;
            _selNote.style.borderLeftWidth = 3;
            _selNote.style.borderLeftColor = Accent;
            _selNote.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);

            var space = Row(_selBox, 8);
            Text(space, "Numbers:", 14f, Dim).style.marginRight = 6;
            _spaceBtn = Button(space, "button:space", "In its parent's space", "Switches the numbers between its parent's space (what's saved) and world space.", ToggleSpace, 13.5f);

            _pos = NumberRow(_selBox, "Position (cm)", 0, "pos");
            _rot = NumberRow(_selBox, "Turn (degrees)", 1, "rot");
            _scaleBlock = Box(_selBox, "size");
            _scl = NumberRow(_scaleBlock, "Size (%)", 2, "size");
            _scaleNote = Text(_selBox, "It can't be sized.", 13f, Dim);
            _scaleNote.style.marginTop = 6;

            _shotBox = Box(_selBox, "shot");
            Text(_shotBox, "Field of view (degrees)", 14f, Dim).style.marginTop = 6;
            var fovRow = Row(_shotBox);
            fovRow.style.flexWrap = Wrap.NoWrap;
            _fovBox = Number(fovRow, "fov", "", 3, 0, Ink);
            var shotButtons = Row(_shotBox, 4);
            Button(shotButtons, "button:look-through", "Look through it", "Puts the free camera where this shot's camera is, to see what it films.", LookThroughShot);
            Button(shotButtons, "button:set-from-view", "Set from my view", "This shot films from where the free camera is now (and its field of view). Only this shot changes.", ShotFromView);

            var buttons = Row(_selBox, 10);
            Button(buttons, "button:frame", "Frame it (F)", "Moves the free camera to look at it.", FrameSelected);
            Button(buttons, "button:reset-one", "Reset this one", "Puts it back where the scene built it (Ctrl+Z undoes).", ResetSelected);

            _rightTab = Button(_root, "show:right", "< Selected", "Shows the selected thing's panel.", () => SetRightHidden(false), 14f);
            _rightTab.el.style.position = Position.Absolute;
            _rightTab.el.style.right = Gap;
            _rightTab.el.style.top = Gap;
            _rightTab.el.style.display = DisplayStyle.None;
        }

        void BuildTimeline()
        {
            _top = Box(_root, "timeline");
            Panel(_top);
            _top.style.top = Gap;
            var row = Row(_top);
            row.style.flexWrap = Wrap.NoWrap;
            _playBtn = Button(row, "button:play", "Play from here (Space)", "Plays the opening from this moment through its own shots; Space again pauses.", TogglePlay);
            Button(row, "button:prev-beat", "< Beat", "The beat before (the left arrow key).", () => StepBeat(-1));
            Button(row, "button:next-beat", "Beat >", "The next beat (the right arrow key).", () => StepBeat(1));
            _timeLabel = Text(row, "", 16f, Ink);
            _timeLabel.style.marginLeft = 8;
            _timeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _beatLabel = Text(row, "", 16f, Accent, true);
            _beatLabel.style.marginLeft = 10;
            _beatLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _beatLabel.style.overflow = Overflow.Hidden;
            _beatLabel.style.flexShrink = 1;

            _track = Box(_top, "track");
            _track.style.height = 26;
            _track.style.marginTop = 6;
            _track.generateVisualContent += DrawTrack;
            _ctls.Add(new Ctl { id = "track", el = _track, track = true, tip = "Click or drag along the bar to go to any moment (the marks are the beats; the lit one is where you are)." });

            _beatRow = Row(_top, 4);
            BuildBeatChips();
        }

        void BuildBeatChips()
        {
            if (_beatRow == null) return;
            foreach (var c in _beatCtls) _ctls.Remove(c);
            _beatCtls.Clear();
            _beatRow.Clear();
            for (int i = 0; i < _beats.Count; i++)
            {
                int k = i;
                var c = Button(_beatRow, "beat:" + _beats[i].name, _beats[i].name, "Jumps to \"" + _beats[i].name + "\" (" + Clock(_beats[i].time) + ").", () => GoToBeat(k), 13f);
                c.el.style.paddingLeft = c.el.style.paddingRight = 5;
                c.el.style.paddingTop = c.el.style.paddingBottom = 1;
                c.el.style.marginTop = c.el.style.marginBottom = 1;
                c.el.style.marginRight = 3;
                _beatCtls.Add(c);
            }
            _shownBeat = -2;
        }

        void BuildBottom()
        {
            _bottom = Box(_root, "tools");
            Panel(_bottom);
            _bottom.style.left = Gap;
            _bottom.style.right = Gap;
            _bottom.style.bottom = Gap;
            _bottom.style.paddingTop = _bottom.style.paddingBottom = 6;
            var tools = Row(_bottom);
            _toolMove = Button(tools, "tool:move", "Move (W)", "Move tool: arrows along X, Y, Z; the middle square moves it across the view.", () => SetTool(Tool.Move, true));
            _toolTurn = Button(tools, "tool:turn", "Turn (E)", "Turn tool: rings around X, Y, Z.", () => SetTool(Tool.Rotate, true));
            _toolSize = Button(tools, "tool:size", "Size (R)", "Size tool: boxes along X, Y, Z; the middle box sizes it all.", () => SetTool(Tool.Scale, true));
            _snapBtn = Button(tools, "button:snap", "Snap: on (X)", "Snapping: moves in 1 cm, turns in 5°, sizes in 5%. Ctrl held while dragging flips it.", () => SetSnap(!_snap));
            Spacer(tools);
            Button(tools, "button:undo", "Undo (Ctrl+Z)", "Undoes the last change.", () => Undo());
            Button(tools, "button:redo", "Redo (Ctrl+Y)", "Redoes what you undid.", () => Redo());
            Spacer(tools);
            _camBtn = Button(tools, "button:camera", "Camera: free (C)", "Switches between the free camera and the opening's own shot camera.", () => SetFree(!_free, true));
            _lightBtn = Button(tools, "button:light", "Work light (L)", "An even light so you can see the set at dark moments (only while editing; never saved).", () => SetWorkLight(!_workLight));
            Spacer(tools);
            _saveBtn = Button(tools, "button:save", "Save (Ctrl+S)", "Saves your changes (closing the editor saves them too).", Save);
            Button(tools, "button:reset-all", "Reset everything...", "Puts every changed thing back where the scene built it (it asks first).", AskResetAll);
            Button(tools, "button:rebuild", "Rebuild with my edits", "Builds the sets again with your changes, so the lighting and the blinds' stripes follow a moved light.", Rebuild);
            Spacer(tools);
            Button(tools, "button:help", "Help (H)", "Every control, on one page.", () => ShowHelp(!_helpShown));
            Button(tools, "button:close", "Close (F9)", "Closes the editor (it saves your changes); the opening plays on from here.", Close);

            _modeLabel = Text(_bottom, "", 14f, Accent, true);
            _modeLabel.style.marginTop = 3;
            _statusBox = Box(_bottom, "status");
            _statusBox.style.marginTop = 3;
            _statusBox.style.paddingLeft = 6;
            _statusBox.style.paddingTop = _statusBox.style.paddingBottom = 2;
            _statusBox.style.borderTopLeftRadius = _statusBox.style.borderTopRightRadius = _statusBox.style.borderBottomLeftRadius = _statusBox.style.borderBottomRightRadius = 3;
            _status = Text(_statusBox, "", 16f, Ink);
            var keys = Text(_bottom,
                "Right mouse held + W A S D, Q E: fly    Wheel: in / out    Middle mouse: pan    Alt + left mouse: orbit    F: frame    " +
                "W E R: move, turn, size    X: snap    C: camera    L: work light    Tab: panels    Space: play    Ctrl+Z / Y    Ctrl+S: save    H: help    F9: close",
                13f, Dim);
            keys.style.marginTop = 3;

            // The tip for what's under the mouse (a button, a box, a handle), next to it.
            _tip = Text(_root, "", 14f, Ink);
            _tip.style.position = Position.Absolute;
            _tip.style.maxWidth = 420;
            _tip.style.backgroundColor = new Color(0.12f, 0.13f, 0.17f, 1f);
            _tip.style.paddingLeft = _tip.style.paddingRight = 8;
            _tip.style.paddingTop = _tip.style.paddingBottom = 4;
            _tip.style.borderTopLeftRadius = _tip.style.borderTopRightRadius = _tip.style.borderBottomLeftRadius = _tip.style.borderBottomRightRadius = 4;
            _tip.style.borderLeftWidth = _tip.style.borderRightWidth = _tip.style.borderTopWidth = _tip.style.borderBottomWidth = 1;
            _tip.style.borderLeftColor = _tip.style.borderRightColor = _tip.style.borderTopColor = _tip.style.borderBottomColor = new Color(1f, 1f, 1f, 0.2f);
            _tip.style.display = DisplayStyle.None;
        }

        static void Spacer(VisualElement row)
        {
            var s = Box(row);
            s.style.width = 10;
        }

        void BuildHelp()
        {
            _help = Box(_root, "help");
            _help.style.position = Position.Absolute;
            _help.style.left = _help.style.right = _help.style.top = _help.style.bottom = 0;
            _help.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _help.style.alignItems = Align.Center;
            _help.style.justifyContent = Justify.Center;
            var box = Box(_help);
            box.style.backgroundColor = PanelBg;
            box.style.width = 1000;
            box.style.paddingLeft = box.style.paddingRight = 24;
            box.style.paddingTop = box.style.paddingBottom = 18;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 8;
            Text(box, "The opening editor", 24f, Accent, true);
            var cols = Row(box, 10);
            cols.style.alignItems = Align.FlexStart;
            cols.style.flexWrap = Wrap.NoWrap;
            var a = Box(cols);
            a.style.width = 470;
            a.style.marginRight = 20;
            var b = Box(cols);
            b.style.width = 470;
            HelpBlock(a, "Look around",
                "Hold the RIGHT mouse button and move the mouse to look.\n" +
                "While holding it: W A S D fly, Q down, E up, Shift 4x faster, the wheel sets the speed.\n" +
                "Mouse wheel: move in and out.\n" +
                "Middle mouse drag: pan.\n" +
                "Alt + left mouse drag: orbit around the selected thing.\n" +
                "F: frame the selected thing.  C: the free camera or the opening's own shot camera.\n" +
                "L: a work light, to see the set at dark moments (only while editing).  Tab: hide or show the panels.");
            HelpBlock(a, "Pick a thing",
                "Click it in the view (a white box shows what a click would pick), or click it in the list on the left. " +
                "Double-click in the list selects and frames it. Shots and lights show as small marks you can click.\n" +
                "Esc: select nothing.");
            HelpBlock(a, "Move, turn, size",
                "W move: drag an arrow (red X, green Y, blue Z) to move along it; the middle square moves it across the view.\n" +
                "E turn: drag a ring.   R size: drag a box; the middle box sizes it all.\n" +
                "X: snapping on or off (1 cm, 5°, 5%). Ctrl held while dragging flips it.  Esc while dragging cancels.\n" +
                "Or the numbers on the right: click one and type (Enter applies, Tab goes to the next, Esc cancels), or drag " +
                "left and right on it to scrub. They're in its parent's space; the button above them switches to the world's.");
            HelpBlock(b, "The timeline (top)",
                "Space: play from here, Space again pauses. The beat buttons jump to a beat; the left and right arrow keys step " +
                "between beats; click or drag along the bar for any moment.");
            HelpBlock(b, "Shots (the cameras)",
                "Pick a shot, press 'Look through it', fly to where it should be, set the field of view, then 'Set from my view'. " +
                "Changing one shot never changes another.");
            HelpBlock(b, "Changes",
                "Ctrl+Z undo, Ctrl+Y (or Ctrl+Shift+Z) redo. 'What you've changed' on the left lists every changed thing, " +
                "with a reset for each. 'Reset everything...' asks first.\n" +
                "Ctrl+S saves; closing (F9) saves too. '*' in the bar below means unsaved changes.");
            Text(box, "Click anywhere, or press H or Esc, to close this.", 14f, Dim).style.marginTop = 10;
            _help.style.display = DisplayStyle.None;
        }

        static void HelpBlock(VisualElement parent, string title, string body)
        {
            Text(parent, title, 17f, Ink, true).style.marginTop = 10;
            Text(parent, body, 14.5f, Dim).style.marginTop = 2;
        }

        void BuildConfirm()
        {
            _confirm = Box(_root, "confirm");
            _confirm.style.position = Position.Absolute;
            _confirm.style.left = _confirm.style.right = _confirm.style.top = _confirm.style.bottom = 0;
            _confirm.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _confirm.style.alignItems = Align.Center;
            _confirm.style.justifyContent = Justify.Center;
            var box = Box(_confirm);
            box.style.backgroundColor = PanelBg;
            box.style.width = 560;
            box.style.paddingLeft = box.style.paddingRight = 20;
            box.style.paddingTop = box.style.paddingBottom = 16;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 8;
            Text(box, "Reset everything?", 20f, Accent, true);
            _confirmText = Text(box, "", 15f, Ink);
            _confirmText.style.marginTop = 6;
            var row = Row(box, 12);
            Button(row, "confirm:yes", "Reset everything (Enter)", "", () => ConfirmResetAll(true), 15f);
            Button(row, "confirm:no", "Cancel (Esc)", "", () => ConfirmResetAll(false), 15f);
            _confirm.style.display = DisplayStyle.None;
        }

        void RemoveUi()
        {
            if (_root != null) _root.RemoveFromHierarchy();
            _root = _view = _tagLayer = _left = _right = _top = _bottom = _help = _confirm = _changes = _track = _beatRow = null;
            _tip = null;
            _shownBottom = -1f;
            _list = null;
            _ctls.Clear();
            _listCtls.Clear();
            _beatCtls.Clear();
            _changeCtls.Clear();
            _rowOf.Clear();
            _hoverCtl = _pressCtl = _lastClickCtl = null;
            _editing = null;
            _helpShown = _confirmShown = false;
            _shownBeat = -2;
            _shownTime = -1f;
        }

        void BuildList()
        {
            if (_list == null || _stage == null) return;
            foreach (var c in _listCtls) _ctls.Remove(c);
            _listCtls.Clear();
            _rowOf.Clear();
            _list.Clear();
            string group = null;
            foreach (var e in _stage.edits.All)
            {
                if (e.group != group)
                {
                    group = e.group;
                    var h = Text(_list, group, 13.5f, new Color(0.62f, 0.75f, 0.95f), true);
                    h.style.marginTop = 8;
                    h.style.marginBottom = 2;
                }
                var item = e;
                var c = ListRow(_list, "list:" + e.id, e.label, "Click: select " + Short(e) + ".  Double-click: select it and frame it.",
                    () => Select(item, true), () =>
                    {
                        Select(item, false);
                        FrameSelected();
                    }, _list.contentViewport);
                _listCtls.Add(c);
                _rowOf[e] = c;
            }
            IgnoreTree(_list);
            RefreshRows();
        }

        static void IgnoreTree(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            foreach (var c in e.Children()) IgnoreTree(c);
            if (e is ScrollView sv)
            {
                sv.contentViewport.pickingMode = PickingMode.Ignore;
                sv.contentContainer.pickingMode = PickingMode.Ignore;
                IgnoreTree(sv.verticalScroller);
                IgnoreTree(sv.horizontalScroller);
            }
        }

        /// <summary>The list's rows: the selected one lit, changed ones marked "*".</summary>
        void RefreshRows()
        {
            foreach (var kv in _rowOf)
            {
                var c = kv.Value;
                c.on = kv.Key == _sel;
                SetText(c.label, (_changedIds.Contains(kv.Key.id) ? "* " : "") + kv.Key.label);
                c.label.style.color = c.on ? SelColor : Ink;
                Paint(c);
            }
        }

        /// <summary>"What you've changed": each changed thing, with its own reset.</summary>
        void RefreshChanges()
        {
            if (_stage == null) return;
            _changedIds.Clear();
            var data = _stage.edits.Capture();
            foreach (var o in data.objects) _changedIds.Add(o.id);
            foreach (var s in data.shots) _changedIds.Add(s.id);
            if (_changes == null) return;
            foreach (var c in _changeCtls) _ctls.Remove(c);
            _changeCtls.Clear();
            _changes.Clear();
            SetText(_changesTitle, "What you've changed (" + _changedIds.Count + ")");
            if (_changedIds.Count == 0) Text(_changes, "Nothing yet.", 13.5f, Dim);
            int shown = 0;
            foreach (var e in _stage.edits.All)
            {
                if (!_changedIds.Contains(e.id)) continue;
                if (shown == 7)
                {
                    Text(_changes, "and " + (_changedIds.Count - shown) + " more (marked * in the list)", 13f, Dim);
                    break;
                }
                shown++;
                var item = e;
                var row = Row(_changes);
                row.style.flexWrap = Wrap.NoWrap;
                var name = ListRow(row, "changed:" + e.id, Short(e), "Select " + Short(e) + ".", () => Select(item, true), () =>
                {
                    Select(item, false);
                    FrameSelected();
                }, null);
                name.el.style.flexGrow = 1;
                name.el.style.flexShrink = 1;
                name.label.style.whiteSpace = WhiteSpace.NoWrap;
                name.label.style.overflow = Overflow.Hidden;
                var reset = Button(row, "reset-changed:" + e.id, "Reset", "Puts " + Short(e) + " back where it was built (Ctrl+Z undoes).", () => ResetOne(item), 12.5f);
                reset.el.style.marginRight = 0;
                _changeCtls.Add(name);
                _changeCtls.Add(reset);
            }
            RefreshRows();
        }

        // ---------- The selected thing's panel ----------

        /// <summary>The selection's target went (a rebuild): find it again by its id, or select nothing.</summary>
        void ValidateSelection()
        {
            if (_sel != null && (_sel.target == null || _stage == null || _stage.edits == null || _stage.edits.Find(_sel.id) != _sel))
            {
                var again = _stage != null && _stage.edits != null ? _stage.edits.Find(_sel.id) : null;
                _sel = again != null && again.target != null ? again : null;
                RefreshSelectionPanel();
            }
            if (_hover != null && _hover.target == null) _hover = null;
            if (_throughShot != null && _throughShot.target == null) _throughShot = null;
        }

        void RefreshSelectionPanel()
        {
            RefreshRows();
            if (_right == null) return;
            var e = _sel != null && _sel.target != null ? _sel : null;
            SetText(_selName, e != null ? e.label : "Nothing selected");
            _noSel.style.display = e == null ? DisplayStyle.Flex : DisplayStyle.None;
            _selBox.style.display = e != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (e == null) return;
            if (_rightHidden) SetRightHidden(false);
            string where = e.bedroom ? "In the bedroom." : "At the title table.";
            if (e.isShot) where += " A camera: one shot of the opening.";
            if (e.id == "phone-held") where = "In the bedroom, in her hand: from when she lifts it to when she puts it down.";
            else if (e.id == "phone") where = "In the bedroom, on the nightstand: before she picks it up and after she puts it down.";
            SetText(_selWhere, where);
            SetText(_selNote, e.note);
            _selNote.style.display = string.IsNullOrEmpty(e.note) ? DisplayStyle.None : DisplayStyle.Flex;
            bool scale = e.canScale && !e.isShot;
            _scaleBlock.style.display = scale ? DisplayStyle.Flex : DisplayStyle.None;
            _scaleNote.style.display = scale || e.isShot ? DisplayStyle.None : DisplayStyle.Flex;
            _shotBox.style.display = e.isShot ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(_spaceBtn.label, _world ? "In world space" : "In " + SpaceName(e) + " space");
            ShowFields();
        }

        string SpaceName(VhEditable e)
        {
            var p = e.target != null ? e.target.parent : null;
            if (p == null) return "the world's";
            if (_stage != null && p == _stage.bedroom) return "the bedroom's";
            if (_stage != null && p == _stage.tableRoot) return "the title set's";
            if (_stage != null)
                foreach (var o in _stage.edits.All)
                    if (o.target == p) return Short(o).ToLowerInvariant() + "'s";
            return p.name + "'s";
        }

        void ToggleSpace()
        {
            _world = !_world;
            RefreshSelectionPanel();
            Status(_world ? "The numbers are in world space now." : "The numbers are in its parent's space now (what's saved).");
        }

        void ShowFields()
        {
            ValidateSelectionQuiet();
            if (_sel == null || _sel.target == null || _pos == null) return;
            for (int i = 0; i < 3; i++)
            {
                ShowNumber(_pos[i]);
                ShowNumber(_rot[i]);
                ShowNumber(_scl[i]);
            }
            ShowNumber(_fovBox);
        }

        void ValidateSelectionQuiet()
        {
            if (_sel != null && _sel.target == null) _sel = _stage != null && _stage.edits != null ? _stage.edits.Find(_sel.id) : null;
            if (_sel != null && _sel.target == null) _sel = null;
        }

        void ShowNumber(NumberBox b)
        {
            if (b == null) return;
            if (b == _editing)
            {
                SetText(b.value, _editText + "|");
                return;
            }
            SetText(b.value, Format(b, Value(b)));
        }

        float Value(NumberBox b)
        {
            var t = _sel.target;
            switch (b.group)
            {
                case 0: return (_world ? t.position : t.localPosition)[b.axis] * 100f;
                case 1:
                {
                    float v = (_world ? t.rotation : t.localRotation).eulerAngles[b.axis];
                    return v > 180f ? v - 360f : v;
                }
                case 2: return t.localScale[b.axis] * 100f;
                default: return _sel.fov;
            }
        }

        static string Format(NumberBox b, float v)
        {
            if (Mathf.Abs(v) < 0.005f) v = 0f;
            return v.ToString(b.group == 2 ? "0.#" : "0.0", CultureInfo.InvariantCulture);
        }

        static readonly string[] GroupNames = { "position", "turn", "size", "field of view" };
        static readonly string[] Units = { " cm", "°", "%", "°" };

        static string FieldName(NumberBox b) => b.group == 3 ? "field of view" : GroupNames[b.group] + " " + "XYZ"[b.axis];

        void SetValue(NumberBox b, float v)
        {
            var e = _sel;
            var t = e.target;
            if (b.group == 3)
            {
                e.fov = Mathf.Clamp(v, 10f, 120f);
                _stage.edits.Set(e, t.localPosition, t.localRotation, t.localScale);
                return;
            }
            if (b.group == 2)
            {
                var s = t.localScale;
                s[b.axis] = Mathf.Max(0.01f, v / 100f);
                _stage.edits.Set(e, t.localPosition, t.localRotation, s);
                return;
            }
            Vector3 pos = _world ? t.position : t.localPosition;
            Quaternion rot = _world ? t.rotation : t.localRotation;
            if (b.group == 0) pos[b.axis] = v / 100f;
            else
            {
                var eu = rot.eulerAngles;
                eu[b.axis] = v;
                rot = Quaternion.Euler(eu);
            }
            Vector3 wp = pos;
            Quaternion wq = rot;
            if (!_world && t.parent != null)
            {
                wp = t.parent.TransformPoint(pos);
                wq = t.parent.rotation * rot;
            }
            var delta = Matrix4x4.TRS(wp, wq, Vector3.one) * Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
            _stage.edits.MoveBy(e, delta);
        }

        void NumberPress(NumberBox b)
        {
            _numberScrub = false;
            _numberStart = _sel != null && _sel.target != null ? Value(b) : 0f;
        }

        void NumberDrag(bool fine)
        {
            var b = _pressCtl != null ? _pressCtl.number : null;
            if (b == null || _sel == null || _sel.target == null) return;
            float dx = _mouse.x - _pressMouse.x;
            if (!_numberScrub && Mathf.Abs(dx) < 4f) return;
            if (!_numberScrub)
            {
                _numberScrub = true;
                if (_editing != null) CommitNumber();
                PushUndo(null);
            }
            float step = b.group == 3 ? 0.25f : 0.5f;
            if (fine) step *= 0.1f;
            float v = _numberStart + dx * step;
            if (_snap && !fine) v = Round(v, b.group == 1 ? SnapTurn : b.group == 2 ? SnapSize * 100f : 1f);
            SetValue(b, v);
            ShowFields();
            Status(Short(_sel) + ": " + FieldName(b) + " " + Format(b, Value(b)) + Units[b.group] + " (scrubbing; Shift is finer)");
        }

        void NumberRelease()
        {
            _drag = Drag.None;
            var b = _pressCtl != null ? _pressCtl.number : null;
            _pressCtl = null;
            if (b == null || _sel == null || _sel.target == null) return;
            if (_numberScrub)
            {
                _numberScrub = false;
                string what = "set " + Short(_sel) + "'s " + FieldName(b) + " to " + Format(b, Value(b)) + Units[b.group];
                if (_undo.Count > 0)
                {
                    var top = _undo[_undo.Count - 1];
                    top.what = what;
                    _undo[_undo.Count - 1] = top;
                }
                Finished();
                Status(char.ToUpperInvariant(what[0]) + what.Substring(1) + ". (Ctrl+Z undoes.)");
                return;
            }
            BeginEdit(b);
        }

        void BeginEdit(NumberBox b)
        {
            if (_sel == null || _sel.target == null) return;
            _editing = b;
            _editText = Format(b, Value(b));
            _editReplace = true;
            _typed.Clear();
            Paint(b.ctl);
            ShowNumber(b);
            Status("Type the " + FieldName(b) + " in" + Units[b.group] + ", then Enter (Tab: the next box, Esc: cancel).");
        }

        void NumberKeys(Keyboard kb)
        {
            var b = _editing;
            foreach (char c in _typed)
            {
                if (!(char.IsDigit(c) || c == '.' || c == ',' || c == '-' || c == '+')) continue;
                if (_editReplace)
                {
                    _editText = "";
                    _editReplace = false;
                }
                if (_editText.Length < 12) _editText += c == ',' ? '.' : c;
            }
            _typed.Clear();
            if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (_editReplace) _editText = "";
                else if (_editText.Length > 0) _editText = _editText.Substring(0, _editText.Length - 1);
                _editReplace = false;
            }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CommitNumber();
            else if (kb.escapeKey.wasPressedThisFrame)
            {
                _editing = null;
                Paint(b.ctl);
                ShowFields();
                Status("Cancelled: nothing changed.");
            }
            else if (kb.tabKey.wasPressedThisFrame)
            {
                var next = NextBox(b, kb.shiftKey.isPressed ? -1 : 1);
                CommitNumber();
                if (next != null) BeginEdit(next);
            }
            if (_editing != null) ShowNumber(_editing);
        }

        NumberBox NextBox(NumberBox b, int dir)
        {
            var all = new List<NumberBox>();
            all.AddRange(_pos);
            all.AddRange(_rot);
            if (_sel != null && _sel.canScale && !_sel.isShot) all.AddRange(_scl);
            if (_sel != null && _sel.isShot) all.Add(_fovBox);
            int i = all.IndexOf(b);
            if (i < 0) return null;
            return all[(i + dir + all.Count) % all.Count];
        }

        void CommitNumber()
        {
            var b = _editing;
            _editing = null;
            if (b == null) return;
            Paint(b.ctl);
            if (_sel == null || _sel.target == null) return;
            if (!float.TryParse(_editText, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                ShowFields();
                Status("\"" + _editText + "\" isn't a number: nothing changed.");
                return;
            }
            float old = Value(b);
            if (Mathf.Abs(v - old) < 1e-4f)
            {
                ShowFields();
                Status(Short(_sel) + ": " + FieldName(b) + " stays " + Format(b, v) + Units[b.group] + ".");
                return;
            }
            string what = "set " + Short(_sel) + "'s " + FieldName(b) + " to " + Format(b, v) + Units[b.group];
            PushUndo(what);
            SetValue(b, v);
            Finished();
            Status(char.ToUpperInvariant(what[0]) + what.Substring(1) + ". (Ctrl+Z undoes.)" + ReachNote());
        }

        // ---------- Help, the question, the panels ----------

        void ShowHelp(bool on)
        {
            _helpShown = on;
            if (_help != null) _help.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void AskResetAll()
        {
            if (_stage == null) return;
            RefreshChanges();
            if (_changedIds.Count == 0)
            {
                Status("Nothing's changed: everything is where the scene built it.");
                return;
            }
            _confirmShown = true;
            SetText(_confirmText, (_changedIds.Count == 1 ? "Put the 1 changed thing back where the scene built it?" : "Put all " + _changedIds.Count + " changed things back where the scene built them?") +
                                  " (Ctrl+Z still undoes it.)");
            _confirm.style.display = DisplayStyle.Flex;
        }

        void ConfirmResetAll(bool yes)
        {
            _confirmShown = false;
            if (_confirm != null) _confirm.style.display = DisplayStyle.None;
            if (yes) ResetEverything();
            else Status("Nothing was reset.");
        }

        void SetLeftHidden(bool hidden)
        {
            _leftHidden = hidden;
            ApplyPanelPlaces();
        }

        void SetRightHidden(bool hidden)
        {
            _rightHidden = hidden;
            ApplyPanelPlaces();
        }

        void ApplyPanelPlaces()
        {
            if (_left == null) return;
            bool all = _panelsHidden;
            _left.style.display = _leftHidden || all ? DisplayStyle.None : DisplayStyle.Flex;
            _leftTab.el.style.display = _leftHidden && !all ? DisplayStyle.Flex : DisplayStyle.None;
            _right.style.display = _rightHidden || all ? DisplayStyle.None : DisplayStyle.Flex;
            _rightTab.el.style.display = _rightHidden && !all ? DisplayStyle.Flex : DisplayStyle.None;
            _top.style.display = all ? DisplayStyle.None : DisplayStyle.Flex;
            _top.style.left = _leftHidden ? 130f : LeftWidth + Gap * 2f;
            _top.style.right = _rightHidden ? 140f : RightWidth + Gap * 2f;
        }

        // ---------- Status, mode, hints ----------

        void Status(string text)
        {
            _statusText = text ?? "";
            if (_status != null) SetText(_status, _statusText);
            _statusAge = 0f;
        }

        static void SetText(Label l, string s)
        {
            if (l != null && l.text != s) l.text = s;
        }

        void RefreshUi()
        {
            if (_root == null || _opening == null) return;
            ValidateSelection();
            float time = _opening.Time;
            if (Mathf.Abs(time - _shownTime) > 0.04f)
            {
                _shownTime = time;
                SetText(_timeLabel, Clock(time) + " / " + Clock(_opening.Length));
            }
            int beat = BeatIndexAt(time);
            if (beat != _shownBeat)
            {
                _shownBeat = beat;
                SetText(_beatLabel, beat >= 0 ? _beats[beat].name : "Before the first beat");
                for (int i = 0; i < _beatCtls.Count; i++)
                {
                    _beatCtls[i].on = i == beat;
                    Paint(_beatCtls[i]);
                }
            }
            SetText(_playBtn.label, _playing ? "Pause (Space)" : "Play from here (Space)");
            SetOn(_playBtn, _playing);
            SetOn(_toolMove, _tool == Tool.Move);
            SetOn(_toolTurn, _tool == Tool.Rotate);
            SetOn(_toolSize, _tool == Tool.Scale);
            SetText(_snapBtn.label, _snap ? "Snap: on (X)" : "Snap: off (X)");
            SetOn(_snapBtn, _snap);
            SetText(_camBtn.label, _free ? "Camera: free (C)" : "Camera: shot (C)");
            SetOn(_lightBtn, _workLight);
            SetText(_saveBtn.label, _dirty ? "Save (Ctrl+S) *" : "Save (Ctrl+S)");
            SetOn(_saveBtn, _dirty);

            string cam;
            if (!_free) cam = "SHOT CAMERA: " + ShotName(_stage.Pose);
            else if (_throughShot != null) cam = "FREE CAMERA, looking through " + Short(_throughShot);
            else cam = _camAuto ? "FREE CAMERA (on the shot until you move it)" : "FREE CAMERA";
            if (_drag == Drag.Look) cam += " (flying " + _flySpeed.ToString("0.0") + " m/s)";
            string tool = _tool == Tool.Move ? "MOVE" : _tool == Tool.Rotate ? "TURN" : "SIZE";
            SetText(_modeLabel, (_playing ? "PLAYING" : "PAUSED") + "   |   " + cam + "   |   TOOL: " + tool + "   |   SNAP " + (_snap ? "ON" : "OFF") +
                                (_workLight ? "   |   WORK LIGHT" : "") + (_panelsHidden ? "   |   PANELS HIDDEN (Tab)" : "") +
                                "   |   " + (_dirty ? "* UNSAVED CHANGES" : "all saved"));

            _statusAge += Time.unscaledDeltaTime;
            float flash = Mathf.Clamp01(1f - _statusAge / 1.5f);
            _statusBox.style.backgroundColor = new Color(Accent.r, Accent.g, Accent.b, 0.2f * flash);

            // The side panels end above the bottom bar, however tall it is.
            float bh = _bottom.worldBound.height;
            if (!float.IsNaN(bh) && bh > 10f && Mathf.Abs(bh - _shownBottom) > 0.5f)
            {
                _shownBottom = bh;
                _left.style.bottom = bh + Gap * 2f;
                _right.style.maxHeight = Mathf.Max(200f, _root.worldBound.height - bh - Gap * 3f);
            }

            PlaceTip();
        }

        string ShotName(string pose)
        {
            var e = _stage != null ? _stage.edits.Find("shot:" + pose) : null;
            if (e == null) return pose;
            return e.label.StartsWith("Shot: ") ? e.label.Substring(6) : e.label;
        }

        /// <summary>What the thing under the mouse does: a button's or box's tip, a handle, a drag in progress. Shown
        /// next to the mouse (a button's after a moment, so it doesn't flicker as the mouse crosses the bar).</summary>
        string TipNow()
        {
            if (_editing != null) return "Typing: Enter applies, Tab goes to the next box, Esc cancels.";
            switch (_drag)
            {
                case Drag.Look: return "Flying " + _flySpeed.ToString("0.0") + " m/s: W A S D, Q E down and up, Shift 4x; the wheel sets the speed. Let go of the right button to stop.";
                case Drag.Pan: return "Panning (let go of the middle button to stop).";
                case Drag.Orbit: return "Orbiting around " + (_sel != null ? Short(_sel) : "the middle of the view") + ".";
                case Drag.Handle: return "Let go to keep it; Esc cancels; Ctrl flips snapping.";
                case Drag.Ui:
                case Drag.Scrub:
                case Drag.Number:
                case Drag.ListScroll:
                    return "";
            }
            if (_hoverCtl != null && !string.IsNullOrEmpty(_hoverCtl.tip)) return Time.unscaledTime - _hoverSince > 0.45f ? _hoverCtl.tip : "";
            if (_hoverHandle >= 0)
            {
                if (_tool == Tool.Move) return _hoverHandle == 3 ? "Drag to move it across the view." : "Drag to move it along " + AxisNames[_hoverHandle] + ".";
                if (_tool == Tool.Rotate) return "Drag to turn it around " + AxisNames[_hoverHandle] + ".";
                return _hoverHandle == 3 ? "Drag to size it all." : "Drag to size it along " + AxisNames[_hoverHandle] + ".";
            }
            return "";
        }

        void PlaceTip()
        {
            if (_tip == null) return;
            string t = TipNow();
            if (string.IsNullOrEmpty(t) || _helpShown || _confirmShown)
            {
                if (_tip.style.display != DisplayStyle.None) _tip.style.display = DisplayStyle.None;
                return;
            }
            SetText(_tip, t);
            _tip.style.display = DisplayStyle.Flex;
            var area = _root.worldBound;
            Vector2 at = _drag == Drag.Look ? new Vector2(area.center.x - 200f, ViewArea().yMin + 8f) : _mouse + new Vector2(18f, 22f);
            float w = _tip.worldBound.width, h = _tip.worldBound.height;
            if (float.IsNaN(w) || w < 1f) w = 300f;
            if (float.IsNaN(h) || h < 1f) h = 30f;
            // Above the mouse near the bottom (the bar's buttons), to the left near the right edge.
            if (at.y + h > area.yMax - 4f) at.y = _mouse.y - h - 12f;
            if (at.x + w > area.xMax - 4f) at.x = area.xMax - w - 4f;
            _tip.style.left = at.x;
            _tip.style.top = at.y;
        }

        /// <summary>The part of the screen the panels leave for the view (panel coordinates).</summary>
        Rect ViewArea()
        {
            var r = _root != null ? _root.worldBound : new Rect(0f, 0f, 1920f, 1080f);
            float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;
            if (Over(_left, _left != null ? _left.worldBound.center : Vector2.zero)) x0 = Mathf.Max(x0, _left.worldBound.xMax);
            if (Over(_right, _right != null ? _right.worldBound.center : Vector2.zero)) x1 = Mathf.Min(x1, _right.worldBound.xMin);
            if (Over(_top, _top != null ? _top.worldBound.center : Vector2.zero)) y0 = Mathf.Max(y0, _top.worldBound.yMax);
            if (Over(_bottom, _bottom != null ? _bottom.worldBound.center : Vector2.zero)) y1 = Mathf.Min(y1, _bottom.worldBound.yMin);
            if (x1 - x0 < 100f || y1 - y0 < 100f) return r;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        // ---------- Controls: hit-testing, hover, clicks ----------

        Vector2 ScreenToPanel(Vector2 screen)
        {
            if (_root == null || _root.panel == null) return screen;
            return RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        /// <summary>From the panel's coordinates back to the screen's (bottom-left origin, pixels).</summary>
        Vector2 PanelToScreen(Vector2 p)
        {
            if (_root == null || _root.panel == null) return p;
            Vector2 a = RuntimePanelUtils.ScreenToPanel(_root.panel, Vector2.zero);
            Vector2 b = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(Screen.width, Screen.height));
            float kx = (b.x - a.x) / Mathf.Max(1f, Screen.width), ky = (b.y - a.y) / Mathf.Max(1f, Screen.height);
            float sx = (p.x - a.x) / Mathf.Max(1e-5f, kx), sy = (p.y - a.y) / Mathf.Max(1e-5f, ky);
            return new Vector2(sx, Screen.height - sy);
        }

        static bool Contains(Rect r, Vector2 p) => !float.IsNaN(r.width) && !float.IsNaN(r.x) && r.Contains(p);

        static bool Hidden(VisualElement x)
        {
            var d = x.style.display;
            if (d.keyword == StyleKeyword.Undefined && d.value == DisplayStyle.None) return true;
            return x.resolvedStyle.display == DisplayStyle.None;
        }

        static bool Shown(VisualElement e)
        {
            if (e == null || e.panel == null) return false;
            for (var x = e; x != null; x = x.parent)
                if (Hidden(x)) return false;
            return true;
        }

        static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (var p = e; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        Ctl HitControl(Vector2 p)
        {
            if (_root == null || _root.panel == null || _helpShown) return null;
            var top = _confirmShown ? _confirm : TopPanelAt(p);
            if (top == null) return null;
            for (int i = _ctls.Count - 1; i >= 0; i--)
            {
                var c = _ctls[i];
                // Only the panel on top at that point takes the click (one panel may overlap another on a small screen).
                if (!IsInside(c.el, top)) continue;
                if (!Contains(c.el.worldBound, p)) continue;
                if (c.clip != null && !Contains(c.clip.worldBound, p)) continue;
                if (!Shown(c.el)) continue;
                return c;
            }
            return null;
        }

        /// <summary>The panels, bottom to top (the order they were added in).</summary>
        IEnumerable<VisualElement> Panels()
        {
            yield return _left;
            if (_leftTab != null) yield return _leftTab.el;
            yield return _right;
            if (_rightTab != null) yield return _rightTab.el;
            yield return _top;
            yield return _bottom;
        }

        /// <summary>The panel on top at panel point <paramref name="p"/>, or null (the view).</summary>
        VisualElement TopPanelAt(Vector2 p)
        {
            VisualElement top = null;
            foreach (var e in Panels())
                if (Over(e, p)) top = e;
            return top;
        }

        /// <summary>The mouse is over one of the editor's panels (or a modal is up): the view doesn't take it.</summary>
        bool OverUi(Vector2 p)
        {
            if (_root == null) return false;
            if (_helpShown || _confirmShown) return true;
            return Over(_left, p) || Over(_right, p) || Over(_top, p) || Over(_bottom, p) || Over(_leftTab?.el, p) || Over(_rightTab?.el, p);
        }

        static bool Over(VisualElement e, Vector2 p) => e != null && Contains(e.worldBound, p) && Shown(e);

        void SetHoverControl(Ctl c)
        {
            if (c == _hoverCtl) return;
            if (_hoverCtl != null)
            {
                _hoverCtl.hovered = false;
                Paint(_hoverCtl);
            }
            _hoverCtl = c;
            _hoverSince = Time.unscaledTime;
            if (c != null)
            {
                c.hovered = true;
                Paint(c);
            }
        }

        static void SetOn(Ctl c, bool on)
        {
            if (c == null || c.on == on) return;
            c.on = on;
            Paint(c);
        }

        static void Paint(Ctl c)
        {
            if (c == null || c.el == null || c.track || c.scrollbar) return;
            if (c.number != null)
            {
                bool editing = Instance != null && Instance._editing == c.number;
                c.el.style.backgroundColor = c.hovered || editing ? new Color(0.17f, 0.19f, 0.24f) : FieldBg;
                var edge = editing ? Accent : c.hovered ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.12f);
                c.el.style.borderLeftColor = c.el.style.borderRightColor = c.el.style.borderTopColor = c.el.style.borderBottomColor = edge;
                return;
            }
            Color bg;
            if (c.row) bg = c.hovered ? new Color(1f, 1f, 1f, 0.1f) : c.on ? new Color(1f, 0.6f, 0.12f, 0.18f) : RowBg;
            else bg = c.hovered ? (c.on ? new Color(0.34f, 0.52f, 0.78f) : ButtonHover) : c.on ? ButtonOn : ButtonBg;
            c.el.style.backgroundColor = bg;
        }

        void Click(Ctl c)
        {
            bool dbl = c == _lastClickCtl && Time.unscaledTime - _lastClickTime < 0.4f && c.dbl != null;
            _lastClickCtl = dbl ? null : c;
            _lastClickTime = Time.unscaledTime;
            if (dbl) c.dbl();
            else c.click?.Invoke();
        }

        // ---------- The list's scrollbar ----------

        void ScrollListTo(float panelY)
        {
            if (_list == null) return;
            var r = _list.verticalScroller.worldBound;
            if (float.IsNaN(r.height) || r.height < 10f) return;
            // The arrows at both ends aren't part of the run.
            float k = Mathf.Clamp01((panelY - r.yMin - 18f) / Mathf.Max(1f, r.height - 36f));
            float max = Mathf.Max(0f, _list.contentContainer.worldBound.height - _list.contentViewport.worldBound.height);
            _list.scrollOffset = new Vector2(_list.scrollOffset.x, k * max);
        }

        // ---------- The scrub bar ----------

        void DrawTrack(MeshGenerationContext ctx)
        {
            if (_opening == null || _track == null) return;
            var r = _track.contentRect;
            if (float.IsNaN(r.width) || r.width < 2f) return;
            var p = ctx.painter2D;
            float len = Mathf.Max(0.01f, _opening.Length);
            float y0 = r.yMin + 7f, y1 = r.yMax - 7f;
            p.fillColor = new Color(1f, 1f, 1f, 0.12f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, y0));
            p.LineTo(new Vector2(r.xMax, y0));
            p.LineTo(new Vector2(r.xMax, y1));
            p.LineTo(new Vector2(r.xMin, y1));
            p.ClosePath();
            p.Fill();
            float x = r.xMin + r.width * Mathf.Clamp01(_opening.Time / len);
            p.fillColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, y0));
            p.LineTo(new Vector2(x, y0));
            p.LineTo(new Vector2(x, y1));
            p.LineTo(new Vector2(r.xMin, y1));
            p.ClosePath();
            p.Fill();
            p.lineWidth = 2f;
            for (int i = 0; i < _beats.Count; i++)
            {
                float bx = r.xMin + r.width * Mathf.Clamp01(_beats[i].time / len);
                p.strokeColor = i == _shownBeat ? Accent : new Color(1f, 1f, 1f, 0.75f);
                p.BeginPath();
                p.MoveTo(new Vector2(bx, r.yMin + 2f));
                p.LineTo(new Vector2(bx, r.yMax - 2f));
                p.Stroke();
            }
            p.fillColor = Color.white;
            p.BeginPath();
            p.MoveTo(new Vector2(x - 3f, r.yMin));
            p.LineTo(new Vector2(x + 3f, r.yMin));
            p.LineTo(new Vector2(x + 3f, r.yMax));
            p.LineTo(new Vector2(x - 3f, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        // ---------- For tests and the capture tour: where things are on screen ----------

        /// <summary>The screen point (pixels, bottom-left origin, as Mouse.position reads) at the middle of a control:
        /// "button:save", "tool:move", "beat:The grab", "list:phone", "changed:phone", "reset-changed:phone",
        /// "field:pos.x", "field:fov", "button:look-through", "confirm:yes", "track", "show:left"... False when it isn't
        /// shown or laid out.</summary>
        public bool TryScreenPointOf(string id, out Vector2 screen)
        {
            screen = Vector2.zero;
            foreach (var c in _ctls)
            {
                if (c.id != id || !Shown(c.el)) continue;
                var r = c.el.worldBound;
                if (float.IsNaN(r.width) || r.width <= 0f) return false;
                var mid = r.center;
                // Only where a click really reaches it (not clipped by the list, not under another panel).
                if (HitControl(mid) != c) return false;
                screen = PanelToScreen(mid);
                return true;
            }
            return false;
        }

        /// <summary>The screen point on the scrub bar for <paramref name="time"/> seconds.</summary>
        public bool TryTrackScreenPoint(float time, out Vector2 screen)
        {
            screen = Vector2.zero;
            if (_track == null || _opening == null || !Shown(_track)) return false;
            var r = _track.worldBound;
            if (float.IsNaN(r.width) || r.width <= 0f) return false;
            screen = PanelToScreen(new Vector2(r.xMin + r.width * Mathf.Clamp01(time / Mathf.Max(0.01f, _opening.Length)), r.center.y));
            return true;
        }

        /// <summary>The screen point at the middle of a panel ("left", "right", "top", "bottom").</summary>
        public bool TryPanelScreenPoint(string panel, out Vector2 screen)
        {
            screen = Vector2.zero;
            var e = panel == "left" ? _left : panel == "right" ? _right : panel == "top" ? _top : panel == "bottom" ? _bottom : null;
            if (e == null || !Shown(e)) return false;
            var r = e.worldBound;
            if (float.IsNaN(r.width) || r.width <= 0f) return false;
            // A spot on the panel's own background: on top there, and on none of its controls.
            for (int j = 0; j < 12; j++)
                for (int i = 0; i < 12; i++)
                {
                    var q = new Vector2(r.xMin + 4f + (r.width - 8f) * i / 11f, r.yMax - 4f - (r.height - 8f) * j / 11f);
                    if (TopPanelAt(q) != e || HitControl(q) != null) continue;
                    screen = PanelToScreen(q);
                    return true;
                }
            return false;
        }

        /// <summary>The part of the screen the panels leave for the view, in screen pixels (bottom-left origin).</summary>
        public Rect ViewScreenRect()
        {
            var a = ViewArea();
            Vector2 p0 = PanelToScreen(new Vector2(a.xMin, a.yMax)), p1 = PanelToScreen(new Vector2(a.xMax, a.yMin));
            return Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
        }

        /// <summary>True when the screen point is over one of the editor's panels.</summary>
        public bool IsOverPanels(Vector2 screen) => OverUi(ScreenToPanel(screen));
    }
}

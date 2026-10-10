using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// Settings > Controls (docs/SHELL.md "Controls"): a row per gameplay action with a Keyboard &amp; Mouse
    /// column and a Controller column. Select a binding and press the new key or button (Esc, or Start on a
    /// pad, cancels); a key another action already uses asks Swap / Keep both / Undo. Each row has its own
    /// Reset, and Reset all puts everything back. Two steppers choose whether R3 and L3 toggle crouch and
    /// sprint (the default) or hold them. Pad buttons are named as on the connected pad (Xbox or
    /// PlayStation). Changes are saved at once (<see cref="ControlBindings.SaveKey"/> in the save file).
    /// Fully navigable with arrows / d-pad (a grid: up and down between rows, left and right across the
    /// columns). Built in code: no UXML.
    /// </summary>
    public sealed class ControlsScreen : ShellScreen
    {
        public const string BackButton = "back";
        public const string ResetAllButton = "controls-reset-all";
        public const string SwapButton = "controls-swap";
        public const string KeepBothButton = "controls-keep-both";
        public const string UndoButton = "controls-undo";

        public static string SlotButton(string rowId, ControlColumn column) =>
            "controls-" + rowId + (column == ControlColumn.Gamepad ? "-pad" : "-kbm");

        public static string ResetButton(string rowId) => "controls-" + rowId + "-reset";

        public static string StickButton(StickClick which) => which == StickClick.Crouch ? "controls-crouch-stick" : "controls-sprint-stick";

        /// <summary>Clicks closer than this to the end of a rebind are ignored (the key that was just bound
        /// must not open the next one).</summary>
        const float RebindEcho = 0.2f;

        enum State { Idle, Listening, Conflict }

        sealed class RowView
        {
            public ControlRow row;
            public VisualElement line;
            public Button kbm, pad, reset;
        }

        readonly InputService _fixedService;
        readonly List<RowView> _views = new List<RowView>(12);
        readonly List<VisualElement[]> _lines = new List<VisualElement[]>(16);
        readonly List<VisualElement[]> _modalLines = new List<VisualElement[]>(1);
        ScrollView _scroll;
        Label _padHeader, _hint, _modalText;
        Button _crouchStick, _sprintStick, _resetAll, _back, _swap, _keepBoth, _undo;
        VisualElement _modal, _modalBox, _modalButtons;
        PadStyle _style;
        Gamepad _styleFrom;

        State _state;
        InputActionRebindingExtensions.RebindingOperation _op;
        RowView _view;
        ControlColumn _column;
        int _binding, _partEnd;
        bool _actionWasEnabled;
        string _prevOverride, _prevEffective, _pendingPath;
        bool _opDone, _opCompleted;
        ControlConflict _conflict;
        int _endFrame = -100;
        float _endTime = -100f;

        /// <summary>The shell's screen (uses GameRoot.Input).</summary>
        public ControlsScreen() { }

        /// <summary>A screen over another InputService (EditMode tests).</summary>
        public ControlsScreen(InputService service) { _fixedService = service; }

        InputService Service => _fixedService ?? (GameRoot.Exists ? GameRoot.Input : null);
        ControlBindings Model => Service != null ? Service.Bindings : null;

        protected override bool BuildsOwnContent => true;

        /// <summary>Waiting for the new key or button.</summary>
        public bool IsListening => _state == State.Listening;

        /// <summary>The Swap / Keep both / Undo question is open.</summary>
        public bool IsAskingAboutConflict => _state == State.Conflict;

        /// <summary>The question or prompt in the middle of the screen ("" when none).</summary>
        public string PromptText => _state != State.Idle && _modalText != null ? _modalText.text : "";

        /// <summary>The name of the element with keyboard focus ("" if none).</summary>
        public string FocusedName
        {
            get
            {
                var f = Root.focusController != null ? Root.focusController.focusedElement as VisualElement : null;
                return f != null ? f.name ?? "" : "";
            }
        }

        /// <summary>Text on a row's cell (tests): the key or button names shown.</summary>
        public string CellText(string rowId, ControlColumn column)
        {
            var b = Root.Q<Button>(SlotButton(rowId, column));
            return b != null ? b.text : null;
        }

        /// <summary>Shows or hides a row (a template without shooting can hide "fire" and "alt-fire").</summary>
        public void SetRowShown(string rowId, bool shown)
        {
            var row = Model?.FindRow(rowId);
            if (row == null) return;
            row.Shown = shown;
            Refresh();
        }

        // ---------- Building ----------

        protected override void OnInit()
        {
            Root.pickingMode = PickingMode.Position;
            var screen = new VisualElement { name = "controls-screen" };
            screen.AddToClassList("jk-screen");
            screen.AddToClassList("jk-scrim");
            Root.Add(screen);
            var panel = new VisualElement { name = "controls-panel" };
            panel.AddToClassList("jk-panel");
            panel.style.width = 1500;
            panel.style.maxHeight = Length.Percent(96);
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 24;
            screen.Add(panel);

            var title = new Label("Controls");
            title.AddToClassList("jk-heading");
            panel.Add(title);

            var header = Line();
            header.Add(HeaderLabel("", 440));
            header.Add(HeaderLabel("Keyboard & Mouse", -1));
            _padHeader = HeaderLabel("Controller", -1);
            header.Add(_padHeader);
            header.Add(HeaderLabel("", 150));
            panel.Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical) { name = "controls-rows" };
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.style.maxHeight = 560;
            _scroll.style.flexShrink = 1;
            panel.Add(_scroll);

            var model = Model;
            if (model != null)
                foreach (var row in model.Rows) _scroll.Add(MakeRow(row));

            var sticks = Line();
            sticks.style.marginTop = 12;
            _crouchStick = StepperButton(StickButton(StickClick.Crouch), sticks);
            _sprintStick = StepperButton(StickButton(StickClick.Sprint), sticks);
            panel.Add(sticks);

            _hint = new Label("") { name = "controls-hint" };
            _hint.AddToClassList("jk-option-info");
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(_hint);

            var bottom = new VisualElement();
            bottom.AddToClassList("jk-button-row");
            _resetAll = new Button { name = ResetAllButton, text = "Reset all" };
            _resetAll.AddToClassList("jk-button");
            _resetAll.AddToClassList("jk-button--small");
            bottom.Add(_resetAll);
            _back = new Button { name = BackButton, text = "Back" };
            _back.AddToClassList("jk-button");
            _back.AddToClassList("jk-button--small");
            bottom.Add(_back);
            panel.Add(bottom);

            BuildModal();

            foreach (var v in _views)
            {
                var view = v;
                Bind(view.kbm.name, () => BeginRebind(view, ControlColumn.KeyboardMouse));
                Bind(view.pad.name, () => BeginRebind(view, ControlColumn.Gamepad));
                Bind(view.reset.name, () => ResetRow(view));
                _lines.Add(new VisualElement[] { view.kbm, view.pad, view.reset });
            }
            Bind(_crouchStick.name, () => FlipStick(StickClick.Crouch));
            Bind(_sprintStick.name, () => FlipStick(StickClick.Sprint));
            _lines.Add(new VisualElement[] { _crouchStick, _sprintStick });
            Bind(ResetAllButton, ResetAll);
            Bind(BackButton, () => Ui.CloseOverlay());
            _lines.Add(new VisualElement[] { _resetAll, _back });
            Bind(SwapButton, () => ResolveConflict(1));
            Bind(KeepBothButton, () => ResolveConflict(0));
            Bind(UndoButton, () => ResolveConflict(-1));
            _modalLines.Add(new VisualElement[] { _swap, _keepBoth, _undo });

            Root.RegisterCallback<NavigationMoveEvent>(OnNavigate, TrickleDown.TrickleDown);
            Refresh();
        }

        static VisualElement Line()
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            return e;
        }

        static Label HeaderLabel(string text, float width)
        {
            var l = new Label(text);
            l.AddToClassList("jk-dim");
            l.style.fontSize = 24;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (width > 0) l.style.width = width;
            else { l.style.flexGrow = 1; l.style.flexBasis = 0; }
            return l;
        }

        VisualElement MakeRow(ControlRow row)
        {
            var view = new RowView { row = row, line = Line() };
            view.line.name = "controls-row-" + row.Id;
            var name = new Label(row.Label) { name = "controls-label-" + row.Id };
            name.style.width = 440;
            name.style.fontSize = 28;
            name.style.paddingLeft = 12;
            view.line.Add(name);
            view.kbm = SlotButtonElement(SlotButton(row.Id, ControlColumn.KeyboardMouse));
            view.pad = SlotButtonElement(SlotButton(row.Id, ControlColumn.Gamepad));
            view.reset = new Button { name = ResetButton(row.Id), text = "Reset" };
            Compact(view.reset, 24);
            view.reset.style.width = 150;
            view.line.Add(view.kbm);
            view.line.Add(view.pad);
            view.line.Add(view.reset);
            foreach (var b in new[] { view.kbm, view.pad, view.reset })
            {
                var target = b;
                target.RegisterCallback<FocusInEvent>(_ => { if (_scroll != null) _scroll.ScrollTo(target); });
            }
            _views.Add(view);
            return view.line;
        }

        static Button SlotButtonElement(string name)
        {
            var b = new Button { name = name, text = "" };
            Compact(b, 28);
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            return b;
        }

        static void Compact(Button b, float fontSize)
        {
            b.AddToClassList("jk-option");
            b.AddToClassList("jk-stepper");
            b.style.fontSize = fontSize;
            b.style.marginTop = 3;
            b.style.marginBottom = 3;
            b.style.marginLeft = 6;
            b.style.marginRight = 6;
            b.style.paddingTop = 6;
            b.style.paddingBottom = 6;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        Button StepperButton(string name, VisualElement parent)
        {
            var b = new Button { name = name, text = "" };
            Compact(b, 26);
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            parent.Add(b);
            return b;
        }

        void BuildModal()
        {
            _modal = new VisualElement { name = "controls-modal", pickingMode = PickingMode.Position };
            _modal.AddToClassList("jk-screen");
            _modal.AddToClassList("jk-scrim");
            _modal.style.display = DisplayStyle.None;
            _modalBox = new VisualElement { name = "controls-prompt", focusable = true };
            _modalBox.AddToClassList("jk-panel");
            _modalBox.style.width = 980;
            _modalText = new Label("") { name = "controls-prompt-text" };
            _modalText.AddToClassList("jk-text");
            _modalText.style.whiteSpace = WhiteSpace.Normal;
            _modalText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _modalBox.Add(_modalText);
            _modalButtons = new VisualElement();
            _modalButtons.AddToClassList("jk-button-row");
            _modalButtons.style.marginTop = 16;
            _swap = ModalButton(SwapButton, "Swap");
            _keepBoth = ModalButton(KeepBothButton, "Keep both");
            _undo = ModalButton(UndoButton, "Undo");
            _modalBox.Add(_modalButtons);
            _modal.Add(_modalBox);
            Root.Add(_modal);
        }

        Button ModalButton(string name, string text)
        {
            var b = new Button { name = name, text = text };
            b.AddToClassList("jk-button");
            b.AddToClassList("jk-button--small");
            _modalButtons.Add(b);
            return b;
        }

        // ---------- Showing ----------

        protected override void OnShow()
        {
            _style = ControlNames.DetectPadStyle();
            _styleFrom = Gamepad.current;
            Refresh();
        }

        protected override void OnHide()
        {
            // Closed mid-rebind: stop listening; an open conflict keeps both (no further composite parts).
            if (_state == State.Listening) StopListening(false);
            else if (_state == State.Conflict)
            {
                _conflict = default;
                EndRebind();
            }
            CloseModal();
            _state = State.Idle;
        }

        /// <summary>Rewrites every cell, the steppers and the header (after any change).</summary>
        public void Refresh()
        {
            var model = Model;
            if (model == null || _padHeader == null) return;
            _padHeader.text = "Controller (" + ControlNames.StyleName(_style) + ")";
            bool anyConflict = false;
            foreach (var v in _views)
            {
                SetShown(v.line, v.row.Shown);
                anyConflict |= Cell(model, v, ControlColumn.KeyboardMouse, v.kbm);
                anyConflict |= Cell(model, v, ControlColumn.Gamepad, v.pad);
            }
            Stepper(model, StickClick.Crouch, _crouchStick, "Crouch / Slide on ");
            Stepper(model, StickClick.Sprint, _sprintStick, "Sprint on ");
            if (_hint != null)
                _hint.text = anyConflict
                    ? "<color=#ff6b6b>(!)</color> marks a key or button that two actions share."
                    : "Select a binding, then press the new key or button. Esc, or Start on a pad, cancels.";
        }

        bool Cell(ControlBindings model, RowView v, ControlColumn column, Button b)
        {
            var row = v.row;
            bool fixedCell = row.IsFixed(column);
            b.SetEnabled(!fixedCell);
            b.style.opacity = fixedCell ? 0.6f : 1f;
            string primary = model.CellName(row, column, _style, out string extras);
            string text = primary.Length > 0 ? Escape(primary) : "<alpha=#77>(none)</alpha>";
            if (extras.Length > 0) text += "  <size=75%><alpha=#99>" + Escape(extras) + "</alpha></size>";
            bool conflict = !fixedCell && model.HasConflict(row, column);
            if (conflict) text += "  <color=#ff6b6b>(!)</color>";
            b.text = text;
            return conflict;
        }

        static string Escape(string s) => s.IndexOf('<') >= 0 ? "<noparse>" + s + "</noparse>" : s;

        void Stepper(ControlBindings model, StickClick which, Button b, string label)
        {
            if (b == null) return;
            bool has = model.HasStickChoice(which);
            SetShown(b, has);
            if (!has) return;
            string stick = ControlNames.PathName(ControlBindings.StickPath(which), _style);
            b.text = label + stick + ":   <  " + (model.StickHolds(which) ? "Hold" : "Toggle") + "  >";
        }

        protected override VisualElement FirstFocusable()
        {
            if (_state == State.Listening) return _modalBox;
            if (_state == State.Conflict) return _conflict.Exact ? _swap : _keepBoth;
            foreach (var line in _lines)
                foreach (var e in line)
                    if (CanFocus(e)) return e;
            return base.FirstFocusable();
        }

        public override void FocusFirst()
        {
            // Always open on the first row (not on whatever had focus last time).
            Root.schedule.Execute(() =>
            {
                if (Root.style.display == DisplayStyle.None) return;
                FirstFocusable()?.Focus();
            });
            FirstFocusable()?.Focus();
        }

        static bool CanFocus(VisualElement e)
        {
            if (e == null || !e.enabledInHierarchy || !e.canGrabFocus) return false;
            for (var p = e; p != null; p = p.parent)
                if (p.style.display == DisplayStyle.None) return false;
            return true;
        }

        // ---------- Navigation: a grid ----------

        void OnNavigate(NavigationMoveEvent e)
        {
            if (_state == State.Listening)
            {
                Swallow(e);
                return;
            }
            var lines = _state == State.Conflict ? _modalLines : _lines;
            var focused = Root.focusController != null ? Root.focusController.focusedElement as VisualElement : null;
            if (!Locate(lines, focused, out int li, out int ci))
            {
                if (_state == State.Conflict) { _swap.Focus(); Swallow(e); }
                return;
            }
            VisualElement target = null;
            switch (e.direction)
            {
                case NavigationMoveEvent.Direction.Up: target = Vertical(lines, li, ci, -1); break;
                case NavigationMoveEvent.Direction.Down: target = Vertical(lines, li, ci, 1); break;
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Right:
                    int dir = e.direction == NavigationMoveEvent.Direction.Right ? 1 : -1;
                    if (focused == _crouchStick || focused == _sprintStick)
                    {
                        // Left / right on a stepper changes it, like Settings' difficulty; past the end moves on.
                        var which = focused == _crouchStick ? StickClick.Crouch : StickClick.Sprint;
                        bool holds = Model.StickHolds(which);
                        if ((dir > 0) != holds) { FlipStick(which); Swallow(e); return; }
                    }
                    target = Across(lines[li], ci, dir);
                    break;
                case NavigationMoveEvent.Direction.Next: target = Across(lines[li], ci, 1) ?? Vertical(lines, li, 0, 1); break;
                case NavigationMoveEvent.Direction.Previous: target = Across(lines[li], ci, -1) ?? Vertical(lines, li, 0, -1); break;
            }
            if (target != null)
            {
                target.Focus();
                Ui?.PlayUiSound("ui_move");
            }
            Swallow(e);
        }

        void Swallow(NavigationMoveEvent e)
        {
            e.StopPropagation();
            Root.focusController?.IgnoreEvent(e);
        }

        static bool Locate(List<VisualElement[]> lines, VisualElement focused, out int line, out int column)
        {
            line = column = -1;
            if (focused == null) return false;
            for (int i = 0; i < lines.Count; i++)
                for (int j = 0; j < lines[i].Length; j++)
                    if (lines[i][j] == focused) { line = i; column = j; return true; }
            return false;
        }

        static VisualElement Vertical(List<VisualElement[]> lines, int from, int column, int dir)
        {
            for (int i = from + dir; i >= 0 && i < lines.Count; i += dir)
            {
                var line = lines[i];
                // Same column if it can take focus, else the nearest one in that line.
                int c = Mathf.Min(column, line.Length - 1);
                for (int k = 0; k < line.Length; k++)
                {
                    int a = c - k, b = c + k;
                    if (a >= 0 && CanFocus(line[a])) return line[a];
                    if (b < line.Length && CanFocus(line[b])) return line[b];
                }
            }
            return null;
        }

        static VisualElement Across(VisualElement[] line, int from, int dir)
        {
            for (int j = from + dir; j >= 0 && j < line.Length; j += dir)
                if (CanFocus(line[j])) return line[j];
            return null;
        }

        // ---------- Rebinding ----------

        /// <summary>Starts listening for a new key / button for a row's column (what selecting a cell does).
        /// Returns false if that cell can't be rebound right now.</summary>
        public bool BeginRebind(string rowId, ControlColumn column)
        {
            foreach (var v in _views)
                if (v.row.Id == rowId) return BeginRebind(v, column);
            return false;
        }

        bool BeginRebind(RowView view, ControlColumn column)
        {
            var model = Model;
            if (model == null || _state != State.Idle) return false;
            if (Time.unscaledTime - _endTime < RebindEcho) return false;
            var row = view.row;
            if (row.IsFixed(column)) return false;
            int slot = row.Primary(column);
            if (slot < 0) return false;
            var bs = row.Action.bindings;
            _view = view;
            _column = column;
            if (bs[slot].isComposite)
            {
                _binding = slot + 1;
                _partEnd = slot + 1;
                while (_partEnd < bs.Count && bs[_partEnd].isPartOfComposite) _partEnd++;
                if (_binding >= _partEnd) return false;
            }
            else
            {
                _binding = slot;
                _partEnd = slot + 1;
            }
            _actionWasEnabled = row.Action.enabled;
            Listen();
            return true;
        }

        void Listen()
        {
            var action = _view.row.Action;
            // The Input System only rebinds disabled actions.
            if (action.enabled) action.Disable();
            var b = action.bindings[_binding];
            _prevOverride = b.overridePath;
            _prevEffective = b.effectivePath ?? "";
            _pendingPath = null;
            _opDone = _opCompleted = false;
            _op = action.PerformInteractiveRebinding(_binding)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnApplyBinding((op, path) => _pendingPath = path)
                .OnComplete(op => { _opDone = true; _opCompleted = true; })
                .OnCancel(op => { _opDone = true; _opCompleted = false; });
            if (_column == ControlColumn.KeyboardMouse)
            {
                _op.WithControlsHavingToMatchPath("<Keyboard>")
                    .WithControlsHavingToMatchPath("<Mouse>")
                    .WithControlsExcluding("<Keyboard>/anyKey")
                    .WithControlsExcluding("<Pointer>/press")
                    .WithControlsExcluding("<Mouse>/scroll");
            }
            else
            {
                _op.WithControlsHavingToMatchPath("<Gamepad>")
                    .WithControlsExcluding("<Gamepad>/start");
                // A button action takes buttons, not a stick's directions (a stick action still takes sticks).
                if (action.expectedControlType != "Vector2")
                    _op.WithControlsExcluding("<Gamepad>/leftStick").WithControlsExcluding("<Gamepad>/rightStick");
            }
            _state = State.Listening;
            ShowPrompt();
            _op.Start();
        }

        void ShowPrompt()
        {
            var row = _view.row;
            var bs = row.Action.bindings;
            string what = row.Label;
            if (bs[_binding].isPartOfComposite)
            {
                int first = _binding;
                while (first > 0 && bs[first - 1].isPartOfComposite) first--;
                int count = _partEnd - first;
                what += ": " + (bs[_binding].name ?? "part") + "  (" + (_binding - first + 1) + " of " + count + ")";
            }
            string ask = _column == ControlColumn.KeyboardMouse
                ? "Press a key or mouse button for\n<b>" + what + "</b>\n\n<size=75%>Esc (or Start / " + ControlNames.PadName("buttonEast", _style) + " on a pad) cancels</size>"
                : "Press a button on the controller for\n<b>" + what + "</b>\n\n<size=75%>Start or Esc cancels</size>";
            OpenModal(ask, false);
        }

        void OpenModal(string text, bool buttons)
        {
            _modalText.text = text;
            SetShown(_modalButtons, buttons);
            _modal.style.display = DisplayStyle.Flex;
            _modal.BringToFront();
            // The table can't take focus or clicks under the prompt.
            _scroll.parent.SetEnabled(false);
            if (buttons) SetShown(_swap, _conflict.Exact);
            FocusSoon(buttons ? FirstFocusable() : _modalBox);
        }

        /// <summary>Focus now and again on the panel's next update (an element that was just shown can't
        /// take focus until its style is resolved).</summary>
        void FocusSoon(VisualElement e)
        {
            if (e == null) return;
            e.Focus();
            var state = _state;
            Root.schedule.Execute(() =>
            {
                if (Root.style.display == DisplayStyle.None || _state != state) return;
                e.Focus();
            });
        }

        void CloseModal()
        {
            if (_modal == null) return;
            _modal.style.display = DisplayStyle.None;
            _scroll.parent.SetEnabled(true);
        }

        public override void Tick(float unscaledDt)
        {
            if (_state == State.Listening)
            {
                if (_opDone)
                {
                    FinishListening();
                    return;
                }
                // Start cancels on any pad (it's never bound here); for a keyboard key, B / Circle cancels too.
                var pads = Gamepad.all;
                for (int i = 0; i < pads.Count; i++)
                {
                    var p = pads[i];
                    if (p.startButton.wasPressedThisFrame || (_column == ControlColumn.KeyboardMouse && p.buttonEast.wasPressedThisFrame))
                    {
                        _op?.Cancel();
                        FinishListening();
                        return;
                    }
                }
                return;
            }
            if (_state == State.Idle && Gamepad.current != _styleFrom)
            {
                _styleFrom = Gamepad.current;
                var style = ControlNames.DetectPadStyle();
                if (style != _style)
                {
                    _style = style;
                    Refresh();
                }
            }
        }

        void FinishListening()
        {
            bool completed = _opDone && _opCompleted;
            string path = _pendingPath;
            DisposeOp();
            if (!completed)
            {
                Ui?.PlayUiSound("ui_back");
                EndRebind();
                return;
            }
            var model = Model;
            var action = _view.row.Action;
            if (!string.IsNullOrEmpty(path) && !string.Equals(path, _prevEffective, StringComparison.OrdinalIgnoreCase))
            {
                model.SetPath(action, _binding, path);
                _conflict = model.FindConflict(action, _binding);
                if (_conflict.Exists)
                {
                    AskAboutConflict(path);
                    return;
                }
            }
            Ui?.PlayUiSound("ui_select");
            NextPartOrEnd();
        }

        void StopListening(bool keepGoing)
        {
            if (_op != null && _op.started) _op.Cancel();
            DisposeOp();
            if (!keepGoing) EndRebind();
        }

        void DisposeOp()
        {
            if (_op == null) return;
            _op.Dispose();
            _op = null;
            _opDone = false;
        }

        void AskAboutConflict(string path)
        {
            _state = State.Conflict;
            string mine = ControlNames.PathName(path, _style);
            string theirs = _conflict.Row.Label;
            if (!_conflict.Exact)
            {
                // One control inside the other (d-pad left and Move's d-pad): no swap, the types differ.
                string other = ControlNames.PathName(_conflict.Row.Action.bindings[_conflict.Binding].effectivePath, _style);
                OpenModal("<b>" + Escape(mine) + "</b> overlaps <b>" + theirs + "</b> (" + Escape(other) + "): pressing it does both.", true);
                return;
            }
            string old = ControlNames.PathName(_prevEffective, _style);
            string swap = old.Length > 0
                ? "Swap gives " + theirs + " " + Escape(old) + " instead."
                : "Swap leaves " + theirs + " without it.";
            OpenModal("<b>" + Escape(mine) + "</b> is already used by <b>" + theirs + "</b>.\n<size=80%>" + swap + "</size>", true);
        }

        /// <summary>1 = Swap, 0 = Keep both, -1 = Undo.</summary>
        void ResolveConflict(int choice)
        {
            if (_state != State.Conflict) return;
            var model = Model;
            if (choice > 0 && _conflict.Exact) model.Swap(_conflict, _prevEffective);
            else if (choice < 0)
            {
                // Back to what it was: the previous override, or the default (SetPath drops the override then).
                var action = _view.row.Action;
                model.SetPath(action, _binding, _prevOverride ?? action.bindings[_binding].path);
            }
            _conflict = default;
            if (choice < 0) EndRebind();
            else NextPartOrEnd();
        }

        void NextPartOrEnd()
        {
            if (_binding + 1 < _partEnd)
            {
                _binding++;
                Listen();
                return;
            }
            EndRebind();
        }

        void EndRebind()
        {
            var view = _view;
            if (view != null && _actionWasEnabled && !view.row.Action.enabled) view.row.Action.Enable();
            _state = State.Idle;
            _endFrame = Time.frameCount;
            _endTime = Time.unscaledTime;
            CloseModal();
            Persist();
            Refresh();
            if (view != null && Root.style.display != DisplayStyle.None)
                FocusSoon(_column == ControlColumn.Gamepad ? view.pad : view.kbm);
        }

        // ---------- Resets and the stick clicks ----------

        void ResetRow(RowView view)
        {
            Model?.ResetRow(view.row);
            Persist();
            Refresh();
        }

        void ResetAll()
        {
            Model?.ResetAll();
            Persist();
            Refresh();
        }

        void FlipStick(StickClick which)
        {
            var model = Model;
            if (model == null || !model.HasStickChoice(which)) return;
            model.SetStickHolds(which, !model.StickHolds(which));
            Ui?.PlayUiSound("ui_move");
            Persist();
            Refresh();
        }

        /// <summary>Writes the overrides into the save at once (only for the shell's own input).</summary>
        void Persist()
        {
            if (_fixedService != null || !GameRoot.Exists) return;
            GameRoot.Input.Bindings.SaveTo(GameRoot.Save.Data);
            GameRoot.Save.Save();
        }

        // ---------- Back ----------

        public override bool OnBack()
        {
            // Escape / B that cancelled or made a binding this frame or the last must not also close the screen;
            // while listening, Back is a key like any other (Esc and Start cancel by themselves).
            if (_state == State.Listening) return true;
            if (_state == State.Conflict)
            {
                ResolveConflict(-1);
                return true;
            }
            return Time.frameCount - _endFrame <= 1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using JamKit.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

namespace JamKit.Shell
{
    /// <summary>The two columns of the Controls screen.</summary>
    public enum ControlColumn { KeyboardMouse, Gamepad }

    /// <summary>Which names the pad buttons get: A / B / X / Y or Cross / Circle / Square / Triangle.</summary>
    public enum PadStyle { Xbox, PlayStation }

    /// <summary>The two stick clicks that can toggle or hold: R3 crouches, L3 sprints.</summary>
    public enum StickClick { Crouch, Sprint }

    /// <summary>
    /// One row of the Controls screen: a gameplay action with a readable name and, per column, its
    /// bindings ("slots": a plain binding or a composite's head; the first one is the one you rebind).
    /// </summary>
    public sealed class ControlRow
    {
        readonly string _id;
        readonly string _label;
        readonly InputAction _action;
        internal readonly List<int> kbm = new List<int>(2);
        internal readonly List<int> pad = new List<int>(2);
        /// <summary>Column of every binding of the action (composite parts too), -1 for neither.
        /// Worked out once, from the paths the asset shipped with.</summary>
        internal int[] columns;
        bool _shown;
        readonly string _context;

        internal ControlRow(string id, string label, InputAction action, bool shown, string context)
        {
            _id = id;
            _label = label;
            _action = action;
            _shown = shown;
            _context = context ?? ControlBindings.ContextPlay;
        }

        /// <summary>When the row's keys are in use: walking around ("play"), in an argument ("argument"), or both
        /// ("any": Pause). Rows only clash with rows they're used alongside.</summary>
        public string Context => _context;

        /// <summary>Stable short id ("move", "crouch-toggle"): button names and tests use it.</summary>
        public string Id => _id;
        /// <summary>What the player reads ("Crouch / Slide (hold)").</summary>
        public string Label => _label;
        public InputAction Action => _action;

        /// <summary>False hides the row on the Controls screen (a template without shooting can hide Fire), and a
        /// hidden row's keys don't count as conflicts. Restart and Ghost start hidden: the Gauntlet shows them.</summary>
        public bool Shown
        {
            get => _shown;
            set => _shown = value;
        }

        /// <summary>Binding indices of this column, the rebindable one first.</summary>
        public IReadOnlyList<int> Slots(ControlColumn c) => c == ControlColumn.Gamepad ? pad : kbm;

        /// <summary>The binding the screen rebinds in this column (-1 if none).</summary>
        public int Primary(ControlColumn c)
        {
            var s = Slots(c);
            return s.Count > 0 ? s[0] : -1;
        }

        /// <summary>True when this column can't be rebound (Look on the mouse: the mouse is the mouse).</summary>
        public bool IsFixed(ControlColumn c)
        {
            int i = Primary(c);
            if (i < 0) return true;
            string p = _action.bindings[i].path ?? "";
            return p.IndexOf("/delta", StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("/position", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Another row's binding on the same key or button, or on a control inside the other
    /// (d-pad left is part of the whole d-pad).</summary>
    public struct ControlConflict
    {
        public ControlRow Row;
        public int Binding;
        /// <summary>The very same control (false: one is part of the other). Only an exact clash can swap:
        /// giving a stick-or-d-pad binding a button's path (or back) would break it.</summary>
        public bool Exact;
        public bool Exists => Row != null;
    }

    /// <summary>
    /// Rebinding for the Controls screen (docs/SHELL.md "Controls"): the rows (Move, Look, Jump, Crouch hold
    /// and toggle, Sprint hold and toggle, Interact, Fire, Alt Fire, Pause, and the Gauntlet's Restart run and
    /// Show ghost, hidden unless a scene shows them), conflicts, resets, the stick-click
    /// Hold / Toggle choice, and saving. Overrides are Input System binding overrides
    /// (SaveBindingOverridesAsJson), kept in the save file's entries under <see cref="SaveKey"/> together
    /// with the two Hold / Toggle choices; <see cref="UseSave"/> loads them at boot and again whenever the
    /// save is reloaded (a fresh slot). Built by <see cref="InputService"/> before its actions are enabled.
    /// </summary>
    public sealed class ControlBindings
    {
        /// <summary>The save entry (SaveData.entries) that holds the overrides and the Hold / Toggle choices.</summary>
        public const string SaveKey = "jk_controls";

        public const string R3 = "<Gamepad>/rightStickPress";
        public const string L3 = "<Gamepad>/leftStickPress";
        /// <summary>The whole d-pad as a stick. Not on gameplay Move by default (menus keep it on Navigate);
        /// a 2D template that steers with it turns it on with <see cref="SetMoveDpad"/>.</summary>
        public const string Dpad = "<Gamepad>/dpad";

        readonly InputActionAsset _asset;
        readonly List<ControlRow> _rows = new List<ControlRow>(12);

        // The stick clicks: the toggle action's binding and a hidden extra binding on the hold action.
        // Hold / Toggle swaps their base paths (not overrides), so a reset never undoes the choice.
        readonly InputAction[] _toggleAction = new InputAction[2];
        readonly InputAction[] _holdAction = new InputAction[2];
        readonly int[] _toggleIndex = { -1, -1 };
        readonly int[] _holdIndex = { -1, -1 };
        readonly bool[] _holds = new bool[2];

        SaveService _save;
        string _applied;

        [Serializable]
        sealed class Saved
        {
            public int version = 1;
            public string overrides = "";
            public bool crouchStickHolds;
            public bool sprintStickHolds;
        }

        internal ControlBindings(InputService input)
        {
            _asset = input.Asset;
            AddRow("move", "Move", input.Move);
            AddRow("look", "Look", input.Look);
            AddRow("jump", "Jump / Dash", input.Dash);
            AddRow("crouch", "Crouch / Slide (hold)", input.Crouch);
            AddRow("crouch-toggle", "Crouch / Slide (toggle)", input.CrouchToggle);
            AddRow("sprint", "Sprint (hold)", input.Sprint);
            AddRow("sprint-toggle", "Sprint (toggle)", input.SprintToggle);
            AddRow("peek-left", "Peek left (hold)", input.PeekLeft, false);
            AddRow("peek-right", "Peek right (hold)", input.PeekRight, false);
            AddRow("interact", "Interact", input.Interact);
            AddRow("fire", "Fire", input.Fire);
            AddRow("alt-fire", "Alt Fire", input.AltFire);
            AddRow("pause", "Pause", input.Pause, true, ContextAny);
            AddRow("restart", "Restart run", input.Restart, false);
            AddRow("ghost", "Show ghost", input.Ghost, false);
            AddRow("argue-ask", "Argue: Ask", input.ArgueAsk, false, ContextArgument);
            AddRow("argue-challenge", "Argue: Challenge", input.ArgueChallenge, false, ContextArgument);
            AddRow("argue-concede", "Argue: Concede", input.ArgueConcede, false, ContextArgument);
            AddRow("argue-press", "Argue: Press (hold)", input.ArguePress, false, ContextArgument);
            AddRow("argue-rest", "Argue: Let it rest", input.ArgueRest, false, ContextArgument);

            SetUpStick(StickClick.Crouch, input.CrouchToggle, input.Crouch, R3);
            SetUpStick(StickClick.Sprint, input.SprintToggle, input.Sprint, L3);
            // Move's d-pad, off ("" path) unless a template opts in: an extra binding with a fixed id.
            _move = input.Move;
            _moveDpadIndex = _move != null ? AddEmpty(_move, "Gamepad", "dpad-optin") : -1;
            foreach (var row in _rows) AddMissingSlots(row);
            foreach (var row in _rows) FindSlots(row);
            // Peeking, off until a template turns it on (the slots above were found from the shipped paths).
            _peekRows[0] = FindRow("peek-left");
            _peekRows[1] = FindRow("peek-right");
            for (int k = 0; k < 2; k++)
            {
                var a = k == 0 ? input.PeekLeft : input.PeekRight;
                if (a == null) continue;
                for (int i = 0; i < a.bindings.Count && i < 2; i++) { _peekAction[k * 2 + i] = a; _peekIndex[k * 2 + i] = i; _peekPath[k * 2 + i] = a.bindings[i].path ?? ""; }
            }
            _sprintLb = IndexOfPath(input.Sprint, "<Gamepad>/leftShoulder");
            _jumpRb = IndexOfPath(input.Dash, "<Gamepad>/rightShoulder");
            _sprintAction = input.Sprint;
            _jumpAction = input.Dash;
            _peekOn = true;
            SetPeekOnBumpers(false);
        }

        static int IndexOfPath(InputAction action, string path)
        {
            if (action == null) return -1;
            var bs = action.bindings;
            for (int i = 0; i < bs.Count; i++)
                if (!bs[i].isComposite && !bs[i].isPartOfComposite && string.Equals(bs[i].path, path, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // ---------- Peeking on the bumpers (a template's opt-in) ----------

        readonly ControlRow[] _peekRows = new ControlRow[2];
        readonly InputAction[] _peekAction = new InputAction[4];
        readonly int[] _peekIndex = { -1, -1, -1, -1 };
        readonly string[] _peekPath = new string[4];
        InputAction _sprintAction, _jumpAction;
        int _sprintLb = -1, _jumpRb = -1;
        bool _peekOn;

        /// <summary>True while a template has peeking on (<see cref="SetPeekOnBumpers"/>).</summary>
        public bool PeekOnBumpers => _peekOn;

        /// <summary>
        /// Peeking on (true) or off (false, the default). On: Peek left / right (hold) are bound (Q / LB, F / RB) and shown on
        /// the Controls screen, and the bumpers come off Sprint (LB) and Jump (RB), so a pad sprints with L3 (and Shift on the
        /// keyboard) and jumps with A. Off: the peek rows are hidden and unbound, LB sprints and RB jumps as before. Moves the
        /// default paths only, so the player's own rebinds stay. Not saved: it's the template's choice (Visiting Hours turns it
        /// on at boot; the Movement Lab keeps it off while it runs).
        /// </summary>
        public void SetPeekOnBumpers(bool on)
        {
            if (_peekOn == on) return;
            _peekOn = on;
            for (int i = 0; i < 4; i++)
                if (_peekAction[i] != null && _peekIndex[i] >= 0) SetBasePath(_peekAction[i], _peekIndex[i], on ? _peekPath[i] : "");
            if (_sprintAction != null && _sprintLb >= 0) SetBasePath(_sprintAction, _sprintLb, on ? "" : "<Gamepad>/leftShoulder");
            if (_jumpAction != null && _jumpRb >= 0) SetBasePath(_jumpAction, _jumpRb, on ? "" : "<Gamepad>/rightShoulder");
            for (int k = 0; k < 2; k++) if (_peekRows[k] != null) _peekRows[k].Shown = on;
            _version++;
        }

        public IReadOnlyList<ControlRow> Rows => _rows;

        public ControlRow FindRow(string id)
        {
            foreach (var r in _rows)
                if (r.Id == id) return r;
            return null;
        }

        public ControlRow FindRow(InputAction action)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Action == action) return _rows[i];
            return null;
        }

        public const string ContextPlay = "play";
        public const string ContextArgument = "argument";
        public const string ContextAny = "any";

        /// <summary>The argument battle's rows (shown while one runs: ArgumentHost).</summary>
        public static readonly string[] ArgumentRows = { "argue-ask", "argue-challenge", "argue-concede", "argue-press", "argue-rest" };

        void AddRow(string id, string label, InputAction action, bool shown = true, string context = ContextPlay)
        {
            if (action != null) _rows.Add(new ControlRow(id, label, action, shown, context));
        }

        static bool SameContext(ControlRow a, ControlRow b) =>
            a.Context == ContextAny || b.Context == ContextAny || a.Context == b.Context;

        int _version;

        /// <summary>Goes up on every change (a rebind, a reset, a load, Hold / Toggle): code that shows the
        /// current keys ("R or View restarts") rebuilds its text when it changes.</summary>
        public int Version => _version;

        // ---------- Columns and slots ----------

        static int ColumnOfPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;
            if (StartsWith(path, "<Keyboard>") || StartsWith(path, "<Mouse>") || StartsWith(path, "<Pointer>")) return (int)ControlColumn.KeyboardMouse;
            if (StartsWith(path, "<Gamepad>") || StartsWith(path, "<XInputController>") || StartsWith(path, "<DualShockGamepad>")
                || StartsWith(path, "<DualSenseGamepadHID>") || StartsWith(path, "<SwitchProControllerHID>")) return (int)ControlColumn.Gamepad;
            return -1;
        }

        static bool StartsWith(string s, string prefix) => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        static int ColumnOfBinding(InputBinding b)
        {
            int c = ColumnOfPath(b.path);
            if (c < 0) c = ColumnOfPath(b.overridePath);
            if (c < 0 && !string.IsNullOrEmpty(b.groups))
            {
                if (b.groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0) c = (int)ControlColumn.Gamepad;
                else if (b.groups.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0) c = (int)ControlColumn.KeyboardMouse;
            }
            return c;
        }

        static int[] Columns(InputAction action)
        {
            var bs = action.bindings;
            var cols = new int[bs.Count];
            for (int i = 0; i < bs.Count; i++) cols[i] = -1;
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].isComposite)
                {
                    int c = -1;
                    for (int j = i + 1; j < bs.Count && bs[j].isPartOfComposite; j++)
                    {
                        cols[j] = ColumnOfBinding(bs[j]);
                        if (c < 0) c = cols[j];
                    }
                    cols[i] = c;
                }
                else if (!bs[i].isPartOfComposite) cols[i] = ColumnOfBinding(bs[i]);
            }
            return cols;
        }

        /// <summary>A toggle has no keyboard key by default: give it an empty slot so the player can add one
        /// (bindings can't be added once the actions are enabled).</summary>
        static void AddMissingSlots(ControlRow row)
        {
            var cols = Columns(row.Action);
            bool kbm = false, pad = false;
            for (int i = 0; i < cols.Length; i++)
            {
                if (row.Action.bindings[i].isPartOfComposite) continue;
                if (cols[i] == (int)ControlColumn.KeyboardMouse) kbm = true;
                if (cols[i] == (int)ControlColumn.Gamepad) pad = true;
            }
            if (!kbm) AddEmpty(row.Action, "Keyboard&Mouse", "kbm-slot");
            if (!pad) AddEmpty(row.Action, "Gamepad", "pad-slot");
        }

        static int AddEmpty(InputAction action, string group, string what)
        {
            action.AddBinding(new InputBinding
            {
                path = "",
                groups = group,
                id = DefaultControls.StableId("jamkit/" + action.actionMap.name + "/" + action.name + "/" + what),
            });
            return action.bindings.Count - 1;
        }

        static void FindSlots(ControlRow row)
        {
            row.columns = Columns(row.Action);
            row.kbm.Clear();
            row.pad.Clear();
            var bs = row.Action.bindings;
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].isPartOfComposite) continue;
                if (row.columns[i] == (int)ControlColumn.KeyboardMouse) row.kbm.Add(i);
                else if (row.columns[i] == (int)ControlColumn.Gamepad) row.pad.Add(i);
            }
        }

        /// <summary>The column of one binding of a row's action (composite parts too), or -1.</summary>
        public int ColumnOf(InputAction action, int bindingIndex)
        {
            var row = FindRow(action);
            if (row == null || row.columns == null || bindingIndex < 0 || bindingIndex >= row.columns.Length) return -1;
            return row.columns[bindingIndex];
        }

        // ---------- Stick clicks: Hold or Toggle ----------

        void SetUpStick(StickClick which, InputAction toggle, InputAction hold, string stickPath)
        {
            int k = (int)which;
            if (toggle == null || hold == null) return;
            var bs = toggle.bindings;
            for (int i = 0; i < bs.Count; i++)
                if (string.Equals(bs[i].path, stickPath, StringComparison.OrdinalIgnoreCase)) { _toggleIndex[k] = i; break; }
            if (_toggleIndex[k] < 0) return;
            _toggleAction[k] = toggle;
            _holdAction[k] = hold;
            _holdIndex[k] = AddEmpty(hold, "Gamepad", "stick-hold");
        }

        /// <summary>True when this stick click can switch between Hold and Toggle (the asset has the toggle's
        /// R3 / L3 binding).</summary>
        public bool HasStickChoice(StickClick which) => _toggleIndex[(int)which] >= 0;

        /// <summary>True: the stick click works like the hold binding (crouch / sprint while pressed in).
        /// False (the default): it toggles.</summary>
        public bool StickHolds(StickClick which) => _holds[(int)which];

        /// <summary>The stick click's control path (R3 for crouch, L3 for sprint).</summary>
        public static string StickPath(StickClick which) => which == StickClick.Crouch ? R3 : L3;

        /// <summary>Gives the stick click to the hold action (true) or the toggle action (false). Moves the
        /// default binding, so the player's own rebinds of either action stay as they are.</summary>
        public void SetStickHolds(StickClick which, bool holds)
        {
            int k = (int)which;
            _holds[k] = holds;
            _version++;
            if (!HasStickChoice(which)) return;
            string stick = StickPath(which);
            SetBasePath(_toggleAction[k], _toggleIndex[k], holds ? "" : stick);
            SetBasePath(_holdAction[k], _holdIndex[k], holds ? stick : "");
        }

        static void SetBasePath(InputAction action, int index, string path)
        {
            if (string.Equals(action.bindings[index].path ?? "", path, StringComparison.Ordinal)) return;
            action.ChangeBinding(index).WithPath(path);
        }

        // ---------- Move on the d-pad (a template's opt-in) ----------

        InputAction _move;
        int _moveDpadIndex = -1;
        bool _moveDpad;

        /// <summary>True while a template has put the d-pad on gameplay Move (<see cref="SetMoveDpad"/>).</summary>
        public bool MoveDpad => _moveDpad;

        /// <summary>Puts the d-pad on gameplay Move (true) or takes it off (false, the default). For 2D templates
        /// that steer with it (the bullet hell, Visiting Hours' argument battle): turn it on when the scene (or the
        /// mode) starts, off when it ends. First-person games leave it off, so the d-pad is free for other actions
        /// (the Gauntlet's ghost, the lab's HUD). Not saved: it's the template's choice, not the player's.</summary>
        public void SetMoveDpad(bool on)
        {
            if (_moveDpad == on || _moveDpadIndex < 0) return;
            _moveDpad = on;
            SetBasePath(_move, _moveDpadIndex, on ? Dpad : "");
            _version++;
        }

        // ---------- Rebinding, conflicts, resets ----------

        /// <summary>Points binding <paramref name="index"/> of <paramref name="action"/> at <paramref name="path"/>
        /// ("" unbinds it). Going back to the default path drops the override.</summary>
        public void SetPath(InputAction action, int index, string path)
        {
            if (action == null || index < 0 || index >= action.bindings.Count) return;
            path = path ?? "";
            if (string.Equals(path, action.bindings[index].path ?? "", StringComparison.OrdinalIgnoreCase)) action.RemoveBindingOverride(index);
            else action.ApplyBindingOverride(index, path);
            _version++;
        }

        /// <summary>The first binding of another shown row, in the same column, on the same control as binding
        /// <paramref name="index"/> of <paramref name="action"/>, or on a control inside it or around it (Ghost's
        /// d-pad left inside Move's d-pad). None if it's unbound or either row is hidden.</summary>
        public ControlConflict FindConflict(InputAction action, int index)
        {
            var none = default(ControlConflict);
            if (action == null || index < 0 || index >= action.bindings.Count) return none;
            var mine = FindRow(action);
            if (mine == null || !mine.Shown) return none;
            string path = action.bindings[index].effectivePath;
            int col = ColumnOf(action, index);
            if (string.IsNullOrEmpty(path) || col < 0) return none;
            ControlConflict partial = none;
            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                if (row.Action == action || !row.Shown || !SameContext(mine, row)) continue;
                var bs = row.Action.bindings;
                for (int j = 0; j < bs.Count; j++)
                {
                    if (bs[j].isComposite || row.columns[j] != col) continue;
                    string other = bs[j].effectivePath;
                    if (string.IsNullOrEmpty(other)) continue;
                    if (string.Equals(other, path, StringComparison.OrdinalIgnoreCase))
                        return new ControlConflict { Row = row, Binding = j, Exact = true };
                    if (!partial.Exists && (Inside(path, other) || Inside(other, path)))
                        partial = new ControlConflict { Row = row, Binding = j, Exact = false };
                }
            }
            return partial;
        }

        /// <summary>True if <paramref name="inner"/> names a control inside <paramref name="outer"/>
        /// ("&lt;Gamepad&gt;/dpad/left" inside "&lt;Gamepad&gt;/dpad").</summary>
        static bool Inside(string inner, string outer) =>
            inner.Length > outer.Length + 1 && inner[outer.Length] == '/'
            && inner.StartsWith(outer, StringComparison.OrdinalIgnoreCase);

        /// <summary>True if any binding of this row's column shares its control with another row.</summary>
        public bool HasConflict(ControlRow row, ControlColumn column)
        {
            var bs = row.Action.bindings;
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].isComposite || row.columns[i] != (int)column) continue;
                if (FindConflict(row.Action, i).Exists) return true;
            }
            return false;
        }

        /// <summary>Resolves a conflict by swapping: the other row's binding takes <paramref name="theirNewPath"/>
        /// (the key the rebound action had before).</summary>
        public void Swap(ControlConflict conflict, string theirNewPath)
        {
            if (conflict.Exists) SetPath(conflict.Row.Action, conflict.Binding, theirNewPath);
        }

        public void ResetRow(ControlRow row)
        {
            if (row != null) row.Action.RemoveAllBindingOverrides();
            _version++;
        }

        /// <summary>Every binding back to its default, and both stick clicks back to Toggle.</summary>
        public void ResetAll()
        {
            _asset.RemoveAllBindingOverrides();
            _version++;
            SetStickHolds(StickClick.Crouch, false);
            SetStickHolds(StickClick.Sprint, false);
        }

        /// <summary>True when nothing differs from the defaults (no overrides, both stick clicks toggle).</summary>
        public bool IsDefault => !_holds[0] && !_holds[1] && string.IsNullOrEmpty(_asset.SaveBindingOverridesAsJson());

        // ---------- Saving ----------

        /// <summary>The overrides and the Hold / Toggle choices as JSON ("" when everything is default).</summary>
        public string ToJson()
        {
            string overrides = _asset.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(overrides) && !_holds[0] && !_holds[1]) return "";
            var s = new Saved
            {
                overrides = overrides ?? "",
                crouchStickHolds = _holds[(int)StickClick.Crouch],
                sprintStickHolds = _holds[(int)StickClick.Sprint],
            };
            return JsonUtility.ToJson(s);
        }

        /// <summary>Puts the overrides into <paramref name="data"/>'s entries (removes the entry when
        /// everything is default). The caller writes the save.</summary>
        public void SaveTo(SaveData data)
        {
            if (data == null) return;
            string json = ToJson();
            data.SetEntry(SaveKey, json.Length > 0 ? json : null);
            _applied = data.GetEntry(SaveKey);
        }

        /// <summary>Replaces every override with the ones saved in <paramref name="data"/> (defaults if none).</summary>
        public void LoadFrom(SaveData data)
        {
            string json = data != null ? data.GetEntry(SaveKey) : null;
            _applied = json;
            LoadJson(json);
        }

        /// <summary>Applies <see cref="ToJson"/>'s output (null or "" restores the defaults).</summary>
        public void LoadJson(string json)
        {
            Saved s = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { s = JsonUtility.FromJson<Saved>(json); }
                catch (Exception e) { Debug.LogWarning("JamKit: the saved controls were damaged; using the defaults. " + e.Message); }
            }
            _asset.LoadBindingOverridesFromJson(s != null ? KnownOverrides(s.overrides) : null);
            _version++;
            SetStickHolds(StickClick.Crouch, s != null && s.crouchStickHolds);
            SetStickHolds(StickClick.Sprint, s != null && s.sprintStickHolds);
        }

        [Serializable]
        sealed class OverrideEntry
        {
            public string action;
            public string id;
            public string path;
            public string interactions;
            public string processors;
        }

        [Serializable]
        sealed class OverrideList
        {
            public List<OverrideEntry> bindings = new List<OverrideEntry>();
        }

        /// <summary>The saved overrides minus any whose binding no longer exists (Move's old d-pad binding, removed
        /// on 2026-10-10), so loading them is silent instead of warning once per launch.</summary>
        string KnownOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            OverrideList list;
            try { list = JsonUtility.FromJson<OverrideList>(json); }
            catch (Exception) { return json; }
            if (list == null || list.bindings == null) return json;
            int before = list.bindings.Count;
            list.bindings.RemoveAll(e => e == null || !HasBinding(e.id));
            if (list.bindings.Count == before) return json;
            return list.bindings.Count > 0 ? JsonUtility.ToJson(list) : "";
        }

        bool HasBinding(string id)
        {
            if (!Guid.TryParse(id, out var g)) return false;
            foreach (var map in _asset.actionMaps)
            {
                var bs = map.bindings;
                for (int i = 0; i < bs.Count; i++)
                    if (bs[i].id == g) return true;
            }
            return false;
        }

        /// <summary>Loads the overrides from <paramref name="save"/> now and again whenever it's reloaded
        /// (GameRoot calls this at boot, before the first scene). Null stops listening.</summary>
        public void UseSave(SaveService save)
        {
            if (_save != null) _save.SettingsChanged -= OnSettingsChanged;
            _save = save;
            if (save == null) return;
            save.SettingsChanged += OnSettingsChanged;
            LoadFrom(save.Data);
        }

        void OnSettingsChanged(Settings s)
        {
            // Raised for every slider move too: only reload when the save's entry is a different one
            // (a load or a fresh slot), not the one this class wrote.
            if (_save == null) return;
            string json = _save.Data.GetEntry(SaveKey);
            if (ReferenceEquals(json, _applied)) return;
            LoadFrom(_save.Data);
        }

        // ---------- Names ----------

        /// <summary>What the screen shows for one slot: a key ("Left Ctrl"), a pad button in the pad's
        /// names ("Circle"), a composite as "WASD" / "Arrow keys" ("" when unbound).</summary>
        public string SlotName(InputAction action, int index, PadStyle style)
        {
            var bs = action.bindings;
            if (index < 0 || index >= bs.Count) return "";
            if (!bs[index].isComposite) return ControlNames.PathName(bs[index].effectivePath, style);
            string up = null, down = null, left = null, right = null;
            var others = new List<string>(4);
            for (int j = index + 1; j < bs.Count && bs[j].isPartOfComposite; j++)
            {
                string n = ControlNames.PathName(bs[j].effectivePath, style);
                string part = bs[j].name ?? "";
                if (part.Equals("up", StringComparison.OrdinalIgnoreCase)) up = n;
                else if (part.Equals("down", StringComparison.OrdinalIgnoreCase)) down = n;
                else if (part.Equals("left", StringComparison.OrdinalIgnoreCase)) left = n;
                else if (part.Equals("right", StringComparison.OrdinalIgnoreCase)) right = n;
                else others.Add(n);
            }
            if (up != null && down != null && left != null && right != null)
            {
                if (up == "Up" && left == "Left" && down == "Down" && right == "Right") return "Arrow keys";
                if (up.Length == 1 && left.Length == 1 && down.Length == 1 && right.Length == 1) return up + left + down + right;
                return up + " " + left + " " + down + " " + right;
            }
            if (up != null) others.Insert(0, up);
            if (left != null) others.Insert(others.Count > 0 ? 1 : 0, left);
            if (down != null) others.Add(down);
            if (right != null) others.Add(right);
            return string.Join(" ", others);
        }

        /// <summary>A row's current keys for a hint: "R or View" (keyboard, then pad; "" if the row is unknown).
        /// Allocates: rebuild it when <see cref="Version"/> changes, not every frame.</summary>
        public string KeysText(string rowId, PadStyle style)
        {
            var row = FindRow(rowId);
            if (row == null) return "";
            int k = row.Primary(ControlColumn.KeyboardMouse), p = row.Primary(ControlColumn.Gamepad);
            string key = k >= 0 ? SlotName(row.Action, k, style) : "";
            string pad = p >= 0 ? SlotName(row.Action, p, style) : "";
            if (key.Length == 0) return pad;
            if (pad.Length == 0) return key;
            return key + " or " + pad;
        }

        /// <summary>The cell text: the rebindable slot, then the others ("Left Ctrl", "also C").
        /// <paramref name="extras"/> gets the "also" part ("" if none). Primary is "" when unbound.</summary>
        public string CellName(ControlRow row, ControlColumn column, PadStyle style, out string extras)
        {
            var slots = row.Slots(column);
            extras = "";
            if (slots.Count == 0) return "";
            string primary = SlotName(row.Action, slots[0], style);
            StringBuilder sb = null;
            for (int i = 1; i < slots.Count; i++)
            {
                string n = SlotName(row.Action, slots[i], style);
                if (n.Length == 0) continue;
                if (sb == null) sb = new StringBuilder("also ");
                else sb.Append(", ");
                sb.Append(n);
            }
            if (sb != null) extras = sb.ToString();
            return primary;
        }
    }

    /// <summary>Readable names for control paths, with Xbox or PlayStation names for pad buttons.</summary>
    public static class ControlNames
    {
        /// <summary>PlayStation names when the last used or connected pad is a DualShock / DualSense, else Xbox.</summary>
        public static PadStyle DetectPadStyle() => Gamepad.current is DualShockGamepad ? PadStyle.PlayStation : PadStyle.Xbox;

        public static string StyleName(PadStyle style) => style == PadStyle.PlayStation ? "PlayStation" : "Xbox";

        /// <summary>"&lt;Gamepad&gt;/buttonEast" is "B" or "Circle", "&lt;Keyboard&gt;/leftCtrl" is "Left Ctrl",
        /// "&lt;Mouse&gt;/leftButton" is "Left Mouse" ("" for an empty path).</summary>
        public static string PathName(string path, PadStyle style)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int close = path.IndexOf('>');
            if (path[0] != '<' || close < 0 || close + 2 > path.Length)
                return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            string device = path.Substring(1, close - 1);
            string control = path.Substring(close + 2);
            switch (device)
            {
                case "Keyboard": return KeyName(control);
                case "Mouse":
                case "Pointer": return MouseName(control);
                case "Gamepad":
                case "XInputController":
                case "SwitchProControllerHID": return PadName(control, style);
                case "DualShockGamepad":
                case "DualSenseGamepadHID": return PadName(control, PadStyle.PlayStation);
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        public static string PadName(string control, PadStyle style)
        {
            bool ps = style == PadStyle.PlayStation;
            switch (control)
            {
                case "buttonSouth": return ps ? "Cross" : "A";
                case "buttonEast": return ps ? "Circle" : "B";
                case "buttonWest": return ps ? "Square" : "X";
                case "buttonNorth": return ps ? "Triangle" : "Y";
                case "leftShoulder": return ps ? "L1" : "LB";
                case "rightShoulder": return ps ? "R1" : "RB";
                case "leftTrigger": return ps ? "L2" : "LT";
                case "rightTrigger": return ps ? "R2" : "RT";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                case "start": return ps ? "Options" : "Menu";
                case "select": return ps ? "Share" : "View";
                case "leftStick": return "Left stick";
                case "rightStick": return "Right stick";
                case "dpad": return "D-pad";
                case "dpad/up": return "D-pad up";
                case "dpad/down": return "D-pad down";
                case "dpad/left": return "D-pad left";
                case "dpad/right": return "D-pad right";
                case "leftStick/up": return "Left stick up";
                case "leftStick/down": return "Left stick down";
                case "leftStick/left": return "Left stick left";
                case "leftStick/right": return "Left stick right";
                case "rightStick/up": return "Right stick up";
                case "rightStick/down": return "Right stick down";
                case "rightStick/left": return "Right stick left";
                case "rightStick/right": return "Right stick right";
            }
            return Words(control);
        }

        public static string MouseName(string control)
        {
            switch (control)
            {
                case "leftButton": return "Left Mouse";
                case "rightButton": return "Right Mouse";
                case "middleButton": return "Middle Mouse";
                case "backButton": return "Mouse 4";
                case "forwardButton": return "Mouse 5";
                case "delta": return "Mouse";
                case "position": return "Mouse position";
                case "scroll": return "Scroll wheel";
                case "scroll/up": return "Scroll up";
                case "scroll/down": return "Scroll down";
            }
            return "Mouse " + Words(control);
        }

        public static string KeyName(string control)
        {
            switch (control)
            {
                case "leftCtrl": return "Left Ctrl";
                case "rightCtrl": return "Right Ctrl";
                case "leftShift": return "Left Shift";
                case "rightShift": return "Right Shift";
                case "leftAlt": return "Left Alt";
                case "rightAlt": return "Right Alt";
                case "escape": return "Esc";
                case "space": return "Space";
                case "enter": return "Enter";
                case "tab": return "Tab";
                case "backspace": return "Backspace";
                case "upArrow": return "Up";
                case "downArrow": return "Down";
                case "leftArrow": return "Left";
                case "rightArrow": return "Right";
                case "capsLock": return "Caps Lock";
            }
            // Letters and symbols as the player's keyboard layout prints them (AZERTY's A key reads "Q").
            var kb = Keyboard.current;
            if (kb != null)
            {
                var key = kb.TryGetChildControl<KeyControl>(control);
                if (key != null && !string.IsNullOrEmpty(key.displayName))
                {
                    string shown = key.displayName;
                    return shown.Length == 1 ? shown.ToUpperInvariant() : shown;
                }
            }
            if (control.Length == 1) return control.ToUpperInvariant();
            if (control.StartsWith("digit", StringComparison.Ordinal) && control.Length == 6) return control.Substring(5);
            return Words(control);
        }

        /// <summary>"numpadEnter" to "Numpad Enter".</summary>
        static string Words(string control)
        {
            if (string.IsNullOrEmpty(control)) return "";
            var sb = new StringBuilder(control.Length + 4);
            for (int i = 0; i < control.Length; i++)
            {
                char c = control[i];
                if (c == '/') { sb.Append(' '); continue; }
                if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
                if (char.IsUpper(c) || (char.IsDigit(c) && !char.IsDigit(control[i - 1]))) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

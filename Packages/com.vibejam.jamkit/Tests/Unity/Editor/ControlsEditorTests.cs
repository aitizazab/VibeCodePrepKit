using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Tests.Editor
{
    /// <summary>Settings > Controls without Unity's play mode: the screen lists every action, overrides
    /// survive a save and a load (also for the actions added at runtime), resets, conflicts, the pad's
    /// button names and the stick clicks' Hold / Toggle choice. Runs over both the project's input asset
    /// and the code defaults.</summary>
    public class ControlsEditorTests
    {
        static readonly string[] RowIds =
            { "move", "look", "jump", "crouch", "crouch-toggle", "sprint", "sprint-toggle", "peek-left", "peek-right", "interact", "fire", "alt-fire", "pause", "restart", "ghost",
              "argue-ask", "argue-challenge", "argue-concede", "argue-press", "argue-rest" };

        readonly List<InputService> _made = new List<InputService>();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _made) s.Dispose();
            _made.Clear();
        }

        InputService Make(bool projectAsset)
        {
            InputActionAsset source = null;
            if (projectAsset)
            {
                var assets = Resources.Load<ShellAssets>(ShellAssets.ResourcePath);
                Assert.That(assets, Is.Not.Null, "ShellAssets (run JamKit > Rebuild Shell Scenes)");
                source = assets.input;
                Assert.That(source, Is.Not.Null, "the project's input asset");
            }
            var s = new InputService(source, new GameFlow());
            _made.Add(s);
            return s;
        }

        static int Kbm(InputService s, string row) => s.Bindings.FindRow(row).Primary(ControlColumn.KeyboardMouse);
        static int Pad(InputService s, string row) => s.Bindings.FindRow(row).Primary(ControlColumn.Gamepad);

        static bool HasEffective(InputAction a, string path)
        {
            foreach (var b in a.bindings)
                if (b.effectivePath == path) return true;
            return false;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheScreenBuildsAndListsEveryAction(bool projectAsset)
        {
            var s = Make(projectAsset);
            var screen = new ControlsScreen(s);
            screen.Init(null, null, new VisualElement());

            var labels = new List<string>();
            screen.Root.Query<Label>().ForEach(l => labels.Add(l.text));
            foreach (var label in new[] { "Move", "Look", "Jump / Dash", "Crouch / Slide (hold)", "Crouch / Slide (toggle)", "Sprint (hold)",
                         "Sprint (toggle)", "Interact", "Fire", "Alt Fire", "Pause", "Restart run", "Show ghost", "Keyboard & Mouse" })
                Assert.That(labels, Does.Contain(label), label);

            Assert.That(s.Bindings.Rows.Count, Is.EqualTo(RowIds.Length));
            foreach (var id in RowIds)
            {
                var row = s.Bindings.FindRow(id);
                Assert.That(row, Is.Not.Null, id);
                foreach (var col in new[] { ControlColumn.KeyboardMouse, ControlColumn.Gamepad })
                {
                    Assert.That(screen.Root.Q<Button>(ControlsScreen.SlotButton(id, col)), Is.Not.Null, id + " " + col);
                    Assert.That(row.Primary(col), Is.GreaterThanOrEqualTo(0), id + " has a " + col + " slot to rebind");
                }
                Assert.That(screen.Root.Q<Button>(ControlsScreen.ResetButton(id)), Is.Not.Null, id + " reset");
            }
            foreach (var name in new[] { ControlsScreen.ResetAllButton, ControlsScreen.BackButton, ControlsScreen.StickButton(StickClick.Crouch),
                         ControlsScreen.StickButton(StickClick.Sprint), ControlsScreen.SwapButton, ControlsScreen.KeepBothButton, ControlsScreen.UndoButton })
                Assert.That(screen.Root.Q<Button>(name), Is.Not.Null, name);

            // Readable cells; the mouse look can't be rebound; the toggles have an empty keyboard slot.
            Assert.That(screen.CellText("crouch", ControlColumn.Gamepad), Does.StartWith("B").Or.StartWith("Circle"));
            Assert.That(screen.CellText("crouch-toggle", ControlColumn.Gamepad), Does.Contain("R3"));
            Assert.That(screen.CellText("sprint-toggle", ControlColumn.Gamepad), Does.Contain("L3"));
            Assert.That(screen.CellText("move", ControlColumn.KeyboardMouse), Does.Contain("WASD").Or.Contain("ZQSD").Or.Contain("W"));
            Assert.That(screen.CellText("crouch-toggle", ControlColumn.KeyboardMouse), Does.Contain("(none)"));
            Assert.That(s.Bindings.FindRow("look").IsFixed(ControlColumn.KeyboardMouse), Is.True, "mouse look");
            Assert.That(screen.Root.Q<Button>(ControlsScreen.SlotButton("look", ControlColumn.KeyboardMouse)).enabledSelf, Is.False);
            Assert.That(screen.Root.Q<Button>(ControlsScreen.SlotButton("look", ControlColumn.Gamepad)).enabledSelf, Is.True, "the right stick can move");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OverridesSurviveSaveAndLoad(bool projectAsset)
        {
            var a = Make(projectAsset);
            a.Bindings.SetPath(a.Crouch, Kbm(a, "crouch"), "<Keyboard>/l");
            a.Bindings.SetPath(a.CrouchToggle, Pad(a, "crouch-toggle"), "<Gamepad>/buttonNorth");
            a.Bindings.SetPath(a.Dash, Pad(a, "jump"), "<Gamepad>/rightTrigger");
            a.Bindings.SetStickHolds(StickClick.Sprint, true);
            var data = new SaveData();
            a.Bindings.SaveTo(data);
            Assert.That(data.GetEntry(ControlBindings.SaveKey), Is.Not.Null.And.Not.Empty);

            // Through the save file's own JSON, into a fresh InputService (a new launch).
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var b = Make(projectAsset);
            Assert.That(b.Crouch.bindings[Kbm(b, "crouch")].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"), "fresh defaults");
            b.Bindings.LoadFrom(loaded);
            Assert.That(b.Crouch.bindings[Kbm(b, "crouch")].effectivePath, Is.EqualTo("<Keyboard>/l"), "Crouch, added at runtime to an older asset");
            Assert.That(b.CrouchToggle.bindings[Pad(b, "crouch-toggle")].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"));
            Assert.That(b.Dash.bindings[Pad(b, "jump")].effectivePath, Is.EqualTo("<Gamepad>/rightTrigger"));
            Assert.That(b.Bindings.StickHolds(StickClick.Sprint), Is.True);
            Assert.That(HasEffective(b.Sprint, ControlBindings.L3), Is.True, "L3 holds sprint");
            Assert.That(HasEffective(b.SprintToggle, ControlBindings.L3), Is.False);

            // Loading an empty save puts everything back.
            b.Bindings.LoadFrom(new SaveData());
            Assert.That(b.Bindings.IsDefault, Is.True);
            Assert.That(b.Crouch.bindings[Kbm(b, "crouch")].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
        }

        [Test]
        public void ResetPutsTheDefaultsBack()
        {
            var s = Make(false);
            int crouch = Kbm(s, "crouch"), interact = Kbm(s, "interact");
            s.Bindings.SetPath(s.Crouch, crouch, "<Keyboard>/l");
            s.Bindings.SetPath(s.Interact, interact, "<Keyboard>/f");
            s.Bindings.SetStickHolds(StickClick.Crouch, true);

            s.Bindings.ResetRow(s.Bindings.FindRow("crouch"));
            Assert.That(s.Crouch.bindings[crouch].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"), "Crouch reset");
            Assert.That(s.Interact.bindings[interact].effectivePath, Is.EqualTo("<Keyboard>/f"), "Interact untouched");
            Assert.That(s.Bindings.StickHolds(StickClick.Crouch), Is.True, "a row's reset leaves the Hold / Toggle choice");

            s.Bindings.ResetAll();
            Assert.That(s.Interact.bindings[interact].effectivePath, Is.EqualTo("<Keyboard>/e"));
            Assert.That(s.Bindings.StickHolds(StickClick.Crouch), Is.False);
            Assert.That(s.Bindings.IsDefault, Is.True);
            var data = new SaveData();
            data.SetEntry(ControlBindings.SaveKey, "{}");
            s.Bindings.SaveTo(data);
            Assert.That(data.GetEntry(ControlBindings.SaveKey), Is.Null, "nothing saved for the defaults");

            // Setting a binding back to its default drops the override.
            s.Bindings.SetPath(s.Crouch, crouch, "<Keyboard>/l");
            s.Bindings.SetPath(s.Crouch, crouch, "<Keyboard>/leftCtrl");
            Assert.That(s.Crouch.bindings[crouch].overridePath, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConflictsAreFoundAndSwapped(bool projectAsset)
        {
            var s = Make(projectAsset);
            var m = s.Bindings;
            foreach (var row in m.Rows)
                for (int i = 0; i < row.Action.bindings.Count; i++)
                    Assert.That(m.FindConflict(row.Action, i).Exists, Is.False, "the defaults share nothing: " + row.Id + " " + i);

            // Interact on Left Ctrl: Crouch has it.
            int interact = Kbm(s, "interact");
            m.SetPath(s.Interact, interact, "<Keyboard>/leftCtrl");
            var c = m.FindConflict(s.Interact, interact);
            Assert.That(c.Exists, Is.True);
            Assert.That(c.Row.Id, Is.EqualTo("crouch"));
            Assert.That(m.HasConflict(m.FindRow("interact"), ControlColumn.KeyboardMouse), Is.True);
            Assert.That(m.HasConflict(m.FindRow("interact"), ControlColumn.Gamepad), Is.False, "the pad column is separate");
            m.Swap(c, "<Keyboard>/e");
            Assert.That(s.Crouch.bindings[c.Binding].effectivePath, Is.EqualTo("<Keyboard>/e"), "Crouch took Interact's old key");
            Assert.That(m.FindConflict(s.Interact, interact).Exists, Is.False);

            // A pad button and a move key (a composite part) conflict the same way.
            int jumpPad = Pad(s, "jump");
            m.SetPath(s.Dash, jumpPad, "<Gamepad>/buttonEast");
            Assert.That(m.FindConflict(s.Dash, jumpPad).Row.Id, Is.EqualTo("crouch"));
            int jumpKey = Kbm(s, "jump");
            m.SetPath(s.Dash, jumpKey, "<Keyboard>/w");
            Assert.That(m.FindConflict(s.Dash, jumpKey).Row.Id, Is.EqualTo("move"));
            // The same key on the pad and the keyboard columns, or an unbound slot, is not a conflict.
            Assert.That(m.FindConflict(s.CrouchToggle, Kbm(s, "crouch-toggle")).Exists, Is.False, "(none)");
        }

        [Test]
        public void StickClicksToggleOrHold()
        {
            var s = Make(true);
            var m = s.Bindings;
            Assert.That(m.HasStickChoice(StickClick.Crouch) && m.HasStickChoice(StickClick.Sprint), Is.True);
            Assert.That(HasEffective(s.CrouchToggle, ControlBindings.R3), Is.True, "R3 toggles by default");
            Assert.That(HasEffective(s.Crouch, ControlBindings.R3), Is.False);

            m.SetStickHolds(StickClick.Crouch, true);
            Assert.That(HasEffective(s.Crouch, ControlBindings.R3), Is.True, "R3 holds crouch");
            Assert.That(HasEffective(s.CrouchToggle, ControlBindings.R3), Is.False);
            Assert.That(HasEffective(s.Crouch, "<Gamepad>/buttonEast"), Is.True, "B still holds too");
            foreach (var row in m.Rows)
                for (int i = 0; i < row.Action.bindings.Count; i++)
                    Assert.That(m.FindConflict(row.Action, i).Exists, Is.False, row.Id + " " + i);

            m.SetStickHolds(StickClick.Crouch, false);
            Assert.That(HasEffective(s.CrouchToggle, ControlBindings.R3), Is.True);
            Assert.That(HasEffective(s.Crouch, ControlBindings.R3), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RestartAndGhostRowsAreHiddenUntilAScreenShowsThem(bool projectAsset)
        {
            var s = Make(projectAsset);
            Assert.That(s.Restart, Is.Not.Null, "Restart, added at runtime to an older asset");
            Assert.That(s.Ghost, Is.Not.Null);
            Assert.That(HasEffective(s.Restart, "<Keyboard>/r") && HasEffective(s.Restart, "<Gamepad>/select"), Is.True, "R and View");
            Assert.That(HasEffective(s.Ghost, "<Keyboard>/g") && HasEffective(s.Ghost, "<Gamepad>/dpad/left"), Is.True, "G and d-pad left");
            var restart = s.Bindings.FindRow("restart");
            var ghost = s.Bindings.FindRow("ghost");
            Assert.That(restart.Label, Is.EqualTo("Restart run"));
            Assert.That(ghost.Label, Is.EqualTo("Show ghost"));
            Assert.That(restart.Shown || ghost.Shown, Is.False, "only the Gauntlet shows them");

            var screen = new ControlsScreen(s);
            screen.Init(null, null, new VisualElement());
            var line = screen.Root.Q<VisualElement>("controls-row-restart");
            Assert.That(line.style.display.value, Is.EqualTo(DisplayStyle.None));
            screen.SetRowShown("restart", true);
            Assert.That(restart.Shown, Is.True);
            Assert.That(line.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(screen.CellText("restart", ControlColumn.KeyboardMouse), Is.Not.Empty);
            Assert.That(screen.CellText("restart", ControlColumn.Gamepad), Does.StartWith("View"));
            // (The key's letter follows the keyboard layout, so only the pad half is pinned.)
            Assert.That(s.Bindings.KeysText("restart", PadStyle.Xbox), Does.EndWith(" or View"));
            Assert.That(s.Bindings.KeysText("restart", PadStyle.PlayStation), Does.EndWith(" or Share"));
            screen.SetRowShown("restart", false);
            Assert.That(line.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AddedActionsHaveFixedBindingIds()
        {
            // The project asset lacks Crouch, the toggles, Restart and Ghost (InputService adds them); the code
            // defaults have them. Both must give each binding the same id every time, or saved rebinds get lost.
            var asset1 = Make(true);
            var asset2 = Make(true);
            var code = Make(false);
            foreach (var name in new[] { "Crouch", "CrouchToggle", "SprintToggle", "Restart", "Ghost", "PeekLeft", "PeekRight" })
            {
                var a = asset1.Gameplay.FindAction(name, true);
                var b = asset2.Gameplay.FindAction(name, true);
                var c = code.Gameplay.FindAction(name, true);
                Assert.That(a.bindings[0].id, Is.Not.EqualTo(System.Guid.Empty), name);
                for (int i = 0; i < a.bindings.Count && i < c.bindings.Count; i++)
                {
                    Assert.That(b.bindings[i].id, Is.EqualTo(a.bindings[i].id), name + " " + i + ": the same on every launch");
                    if (c.bindings[i].path == a.bindings[i].path)
                        Assert.That(c.bindings[i].id, Is.EqualTo(a.bindings[i].id), name + " " + i + ": the asset rebuilt from the defaults keeps it");
                }
            }

            // So a Restart rebind made with one survives into another.
            asset1.Bindings.SetPath(asset1.Restart, Kbm(asset1, "restart"), "<Keyboard>/t");
            var data = new SaveData();
            asset1.Bindings.SaveTo(data);
            asset2.Bindings.LoadFrom(data);
            Assert.That(asset2.Restart.bindings[Kbm(asset2, "restart")].effectivePath, Is.EqualTo("<Keyboard>/t"));
            code.Bindings.LoadFrom(data);
            Assert.That(code.Restart.bindings[Kbm(code, "restart")].effectivePath, Is.EqualTo("<Keyboard>/t"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MoveHasNoDpadButNavigateDoes(bool projectAsset)
        {
            var s = Make(projectAsset);
            foreach (var b in s.Move.bindings)
                Assert.That(b.effectivePath ?? "", Does.Not.StartWith(ControlBindings.Dpad), "gameplay Move has no d-pad: " + b.effectivePath);
            Assert.That(HasEffective(s.Move, "<Gamepad>/leftStick"), Is.True, "the left stick steers");
            Assert.That(HasEffective(s.Navigate, ControlBindings.Dpad), Is.True, "menus keep the d-pad");
            var screen = new ControlsScreen(s);
            screen.Init(null, null, new VisualElement());
            Assert.That(screen.CellText("move", ControlColumn.Gamepad), Is.EqualTo("Left stick"), "just the stick");

            // A 2D template's opt-in puts it back, and takes it away again.
            s.Bindings.SetMoveDpad(true);
            Assert.That(HasEffective(s.Move, ControlBindings.Dpad), Is.True);
            s.Bindings.SetMoveDpad(false);
            Assert.That(HasEffective(s.Move, ControlBindings.Dpad), Is.False);
        }

        // The save entry's layout (ControlBindings.Saved) and the Input System's override list, to write an old save.
        [System.Serializable] class SavedControls { public int version = 1; public string overrides = ""; public bool crouchStickHolds; public bool sprintStickHolds; }
        [System.Serializable] class OverrideEntry { public string action, id, path, interactions = "null", processors = "null"; }
        [System.Serializable] class OverrideList { public List<OverrideEntry> bindings = new List<OverrideEntry>(); }

        [Test]
        public void ASavedOverrideOfTheRemovedDpadIsDroppedSilently()
        {
            var s = Make(true);
            int jumpKey = s.Bindings.FindRow("jump").Primary(ControlColumn.KeyboardMouse);
            var list = new OverrideList();
            // The asset's old Move d-pad binding (removed 2026-10-10), as a save from before would have it...
            list.bindings.Add(new OverrideEntry { action = "Gameplay/Move", id = "a47cf3a0-62ca-45b8-9437-b6b603d23541", path = "<Gamepad>/rightStick" });
            // ...next to a good one.
            list.bindings.Add(new OverrideEntry { action = "Gameplay/Dash", id = s.Dash.bindings[jumpKey].id.ToString(), path = "<Keyboard>/f" });
            var data = new SaveData();
            data.SetEntry(ControlBindings.SaveKey, JsonUtility.ToJson(new SavedControls { overrides = JsonUtility.ToJson(list) }));

            var warnings = new List<string>();
            Application.LogCallback grab = (text, trace, type) => { if (type == LogType.Warning || type == LogType.Error) warnings.Add(text); };
            Application.logMessageReceived += grab;
            try { s.Bindings.LoadFrom(data); }
            finally { Application.logMessageReceived -= grab; }
            Assert.That(warnings.Count, Is.EqualTo(0), "no 'Could not override binding' warning: " + string.Join(" | ", warnings));
            Assert.That(s.Dash.bindings[jumpKey].effectivePath, Is.EqualTo("<Keyboard>/f"), "the good override still loads");
            Assert.That(HasEffective(s.Move, "<Gamepad>/rightStick"), Is.False, "the stale one went nowhere");
        }

        [Test]
        public void TheGhostsDpadLeftIsFreeAndRClashesOnlyWhenShown()
        {
            var s = Make(true);
            var m = s.Bindings;
            int ghostPad = Pad(s, "ghost");
            Assert.That(m.FindConflict(s.Ghost, ghostPad).Exists, Is.False, "hidden rows don't count");
            int interact = Kbm(s, "interact");
            m.SetPath(s.Interact, interact, "<Keyboard>/r");
            Assert.That(m.FindConflict(s.Interact, interact).Exists, Is.False, "R is free while Restart is hidden");

            m.FindRow("restart").Shown = true;
            m.FindRow("ghost").Shown = true;
            var c = m.FindConflict(s.Interact, interact);
            Assert.That(c.Exists && c.Exact, Is.True, "R is Restart's in the lab");
            Assert.That(c.Row.Id, Is.EqualTo("restart"));
            m.SetPath(s.Interact, interact, "<Keyboard>/e");

            // Move has no d-pad now, so d-pad left is the ghost's alone; no (!) on either row.
            Assert.That(m.FindConflict(s.Ghost, ghostPad).Exists, Is.False, "d-pad left doesn't move you any more");
            Assert.That(m.HasConflict(m.FindRow("move"), ControlColumn.Gamepad), Is.False);
            Assert.That(m.HasConflict(m.FindRow("ghost"), ControlColumn.Gamepad), Is.False);

            // The nested-clash check stays for rebinds: a template that puts the d-pad back on Move gets it flagged,
            // and it can't be swapped (a button and a stick-like binding).
            m.SetMoveDpad(true);
            c = m.FindConflict(s.Ghost, ghostPad);
            Assert.That(c.Exists, Is.True, "d-pad left inside Move's d-pad");
            Assert.That(c.Row.Id, Is.EqualTo("move"));
            Assert.That(c.Exact, Is.False);
            m.SetMoveDpad(false);
            Assert.That(m.FindConflict(s.Restart, Pad(s, "restart")).Exists, Is.False, "View is free");
            Assert.That(m.FindConflict(s.Restart, Kbm(s, "restart")).Exists, Is.False, "R is free");
            Assert.That(m.FindConflict(s.Ghost, Kbm(s, "ghost")).Exists, Is.False, "G is free");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ArgumentVerbsOnlyClashWithinAnArgument(bool projectAsset)
        {
            var s = Make(projectAsset);
            var m = s.Bindings;
            foreach (var id in ControlBindings.ArgumentRows)
            {
                var row = m.FindRow(id);
                Assert.That(row, Is.Not.Null, id);
                Assert.That(row.Shown, Is.False, id + " shows only while a battle runs");
                Assert.That(row.Context, Is.EqualTo(ControlBindings.ContextArgument));
                row.Shown = true;
            }
            Assert.That(HasEffective(s.ArgueChallenge, "<Gamepad>/rightShoulder") && HasEffective(s.ArgueChallenge, "<Keyboard>/2"), Is.True, "2 / RB challenges (A confirms everywhere)");
            Assert.That(HasEffective(s.ArguePress, "<Gamepad>/buttonEast"), Is.True, "hold B / Circle presses");
            // RB is Dash and Challenge, B is Crouch and Press, X is Interact and Ask: different moments, no clash.
            foreach (var row in m.Rows)
                for (int i = 0; i < row.Action.bindings.Count; i++)
                    Assert.That(m.FindConflict(row.Action, i).Exists, Is.False, row.Id + " " + i);
            // Within an argument they clash as usual; Pause clashes with everything.
            int ask = Kbm(s, "argue-ask");
            m.SetPath(s.ArgueAsk, ask, "<Keyboard>/2");
            Assert.That(m.FindConflict(s.ArgueAsk, ask).Row.Id, Is.EqualTo("argue-challenge"));
            m.SetPath(s.ArgueAsk, ask, "<Keyboard>/p");
            Assert.That(m.FindConflict(s.ArgueAsk, ask).Row.Id, Is.EqualTo("pause"));
            m.SetPath(s.ArgueAsk, ask, "<Keyboard>/e");
            Assert.That(m.FindConflict(s.ArgueAsk, ask).Exists, Is.False, "E is Interact's, but walking around");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PeekingOnTheBumpersTakesThemOffSprintAndJump(bool projectAsset)
        {
            var s = Make(projectAsset);
            var m = s.Bindings;
            const string lb = "<Gamepad>/leftShoulder", rb = "<Gamepad>/rightShoulder";
            // Off (every template but Visiting Hours): LB sprints, RB jumps, the peek rows are hidden and unbound.
            Assert.That(m.PeekOnBumpers, Is.False);
            Assert.That(HasEffective(s.Sprint, lb) && HasEffective(s.Dash, rb), Is.True);
            Assert.That(HasEffective(s.PeekLeft, lb) || HasEffective(s.PeekRight, rb) || HasEffective(s.PeekLeft, "<Keyboard>/q"), Is.False);
            Assert.That(m.FindRow("peek-left").Shown || m.FindRow("peek-right").Shown, Is.False);

            // On: the bumpers peek (Q / F on the keyboard); L3 and Shift still sprint, A and Space still jump.
            m.SetPeekOnBumpers(true);
            Assert.That(HasEffective(s.PeekLeft, lb) && HasEffective(s.PeekLeft, "<Keyboard>/q"), Is.True, "Q / LB peek left");
            Assert.That(HasEffective(s.PeekRight, rb) && HasEffective(s.PeekRight, "<Keyboard>/f"), Is.True, "F / RB peek right");
            Assert.That(HasEffective(s.Sprint, lb), Is.False, "LB is off Sprint");
            Assert.That(HasEffective(s.Sprint, "<Keyboard>/leftShift") && HasEffective(s.SprintToggle, ControlBindings.L3), Is.True, "Shift and L3 sprint");
            Assert.That(HasEffective(s.Dash, rb), Is.False, "RB is off Jump");
            Assert.That(HasEffective(s.Dash, "<Gamepad>/buttonSouth") && HasEffective(s.Dash, "<Keyboard>/space"), Is.True);
            Assert.That(m.FindRow("peek-left").Shown && m.FindRow("peek-right").Shown, Is.True);
            foreach (var row in m.Rows)
                for (int i = 0; i < row.Action.bindings.Count; i++)
                    Assert.That(m.FindConflict(row.Action, i).Exists, Is.False, "nothing shares a key: " + row.Id + " " + i);
            // The argument's Challenge is RB too, but in its own context (the battle reads its own keys).
            foreach (var id in ControlBindings.ArgumentRows) m.FindRow(id).Shown = true;
            Assert.That(m.FindConflict(s.PeekRight, Pad(s, "peek-right")).Exists, Is.False, "RB: peek in the world, Challenge in an argument");
            foreach (var id in ControlBindings.ArgumentRows) m.FindRow(id).Shown = false;

            var screen = new ControlsScreen(s);
            screen.Init(null, null, new VisualElement());
            Assert.That(screen.CellText("peek-left", ControlColumn.Gamepad), Does.StartWith("LB").Or.StartWith("L1"));
            Assert.That(screen.CellText("peek-right", ControlColumn.Gamepad), Does.StartWith("RB").Or.StartWith("R1"));
            Assert.That(screen.CellText("sprint", ControlColumn.Gamepad), Does.Contain("(none)"), "Sprint (hold) has no pad button: L3 is the toggle");
            Assert.That(screen.CellText("sprint-toggle", ControlColumn.Gamepad), Does.Contain("L3"));
            Assert.That(screen.Root.Q<VisualElement>("controls-row-peek-left").style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // A rebind of the peek survives a save and a load.
            m.SetPath(s.PeekRight, Kbm(s, "peek-right"), "<Keyboard>/e");
            var data = new SaveData();
            m.SaveTo(data);
            var b = Make(projectAsset);
            b.Bindings.SetPeekOnBumpers(true);
            b.Bindings.LoadFrom(data);
            Assert.That(b.PeekRight.bindings[Kbm(b, "peek-right")].effectivePath, Is.EqualTo("<Keyboard>/e"));

            // Off again: as before.
            m.SetPeekOnBumpers(false);
            Assert.That(HasEffective(s.Sprint, lb) && HasEffective(s.Dash, rb), Is.True);
            Assert.That(HasEffective(s.PeekLeft, lb), Is.False);
            Assert.That(m.FindRow("peek-left").Shown, Is.False);
        }

        [Test]
        public void PadButtonsAreNamedForXboxAndPlayStation()
        {
            Assert.That(ControlNames.PathName("<Gamepad>/buttonSouth", PadStyle.Xbox), Is.EqualTo("A"));
            Assert.That(ControlNames.PathName("<Gamepad>/buttonSouth", PadStyle.PlayStation), Is.EqualTo("Cross"));
            Assert.That(ControlNames.PathName("<Gamepad>/buttonEast", PadStyle.Xbox), Is.EqualTo("B"));
            Assert.That(ControlNames.PathName("<Gamepad>/buttonEast", PadStyle.PlayStation), Is.EqualTo("Circle"));
            Assert.That(ControlNames.PathName("<Gamepad>/buttonWest", PadStyle.PlayStation), Is.EqualTo("Square"));
            Assert.That(ControlNames.PathName("<Gamepad>/buttonNorth", PadStyle.Xbox), Is.EqualTo("Y"));
            Assert.That(ControlNames.PathName("<Gamepad>/leftShoulder", PadStyle.Xbox), Is.EqualTo("LB"));
            Assert.That(ControlNames.PathName("<Gamepad>/leftShoulder", PadStyle.PlayStation), Is.EqualTo("L1"));
            Assert.That(ControlNames.PathName("<Gamepad>/rightTrigger", PadStyle.PlayStation), Is.EqualTo("R2"));
            Assert.That(ControlNames.PathName("<Gamepad>/rightStickPress", PadStyle.Xbox), Is.EqualTo("R3"));
            Assert.That(ControlNames.PathName("<Gamepad>/rightStickPress", PadStyle.PlayStation), Is.EqualTo("R3"));
            Assert.That(ControlNames.PathName("<Gamepad>/start", PadStyle.Xbox), Is.EqualTo("Menu"));
            Assert.That(ControlNames.PathName("<Gamepad>/start", PadStyle.PlayStation), Is.EqualTo("Options"));
            Assert.That(ControlNames.PathName("<Gamepad>/leftStick", PadStyle.Xbox), Is.EqualTo("Left stick"));
            Assert.That(ControlNames.PathName("<Keyboard>/leftCtrl", PadStyle.Xbox), Is.EqualTo("Left Ctrl"));
            Assert.That(ControlNames.PathName("<Mouse>/leftButton", PadStyle.Xbox), Is.EqualTo("Left Mouse"));
            Assert.That(ControlNames.PathName("", PadStyle.Xbox), Is.EqualTo(""));
        }
    }
}

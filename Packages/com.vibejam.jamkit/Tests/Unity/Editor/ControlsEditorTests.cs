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
            { "move", "look", "jump", "crouch", "crouch-toggle", "sprint", "sprint-toggle", "interact", "fire", "alt-fire", "pause" };

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
                         "Sprint (toggle)", "Interact", "Fire", "Alt Fire", "Pause", "Keyboard & Mouse" })
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

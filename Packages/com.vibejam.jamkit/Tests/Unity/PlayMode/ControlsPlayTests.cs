using System.Collections;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.Play
{
    /// <summary>
    /// A test keyboard whose keys reach the game's actions in a batch-mode test run. With no focused Game View
    /// the editor's default (PointersAndKeyboardsRespectGameViewFocus) sends keyboard events to the editor's
    /// state, not the game's: an InputSystem.onEvent listener (the rebind) sees the key but the actions never
    /// do. This routes all input to the game, as a focused window or a built player does, with a copy of the
    /// input settings that <see cref="Dispose"/> puts back (and removes the device).
    /// </summary>
    public sealed class TestKeyboard : System.IDisposable
    {
        readonly InputSettings _saved, _test;
        public readonly Keyboard Device;

        public TestKeyboard(string name)
        {
            _saved = InputSystem.settings;
            _test = Object.Instantiate(_saved);
            _test.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _test.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _test;
            Device = InputSystem.AddDevice<Keyboard>(name);
        }

        /// <summary>Holds exactly these keys (none: all up) from the next input update.</summary>
        public void Set(params Key[] keys) => InputSystem.QueueStateEvent(Device, new KeyboardState(keys));

        /// <summary>Holds exactly these keys, then waits two frames so the actions have seen it.</summary>
        public IEnumerator Press(params Key[] keys)
        {
            Set(keys);
            yield return null;
            yield return null;
        }

        public void Dispose()
        {
            if (Device != null && Device.added) InputSystem.RemoveDevice(Device);
            if (InputSystem.settings == _test) InputSystem.settings = _saved;
            if (_test != null) Object.Destroy(_test);
        }
    }

    /// <summary>Settings > Controls in the running shell: a real interactive rebind of Crouch on a test
    /// keyboard, then Crouch answers the new key and not the old one, the rebind is saved, Reset undoes it.</summary>
    public class ControlsPlayTests
    {
        TestKeyboard _keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _keyboard = new TestKeyboard("ControlsTestKeyboard");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _keyboard?.Dispose();
            _keyboard = null;
            if (!GameRoot.Exists) return;
            GameRoot.Ui.CloseOverlays();
            GameRoot.Input.Bindings.ResetAll();
        }

        IEnumerator Keys(params Key[] keys) => _keyboard.Press(keys);

        [UnityTest]
        public IEnumerator RebindCrouchOnTheKeyboard()
        {
            var ui = GameRoot.Ui;
            var input = GameRoot.Input;
            ui.OpenSettings();
            Assert.That(ui.Settings.Activate("controls"), Is.True, "Settings has Controls");
            Assert.That(ui.IsOverlayOpen(ui.Controls), Is.True);
            string first = ControlsScreen.SlotButton("move", ControlColumn.KeyboardMouse);
            yield return WaitFor(() => ui.Controls.FocusedName == first, "focus on the first row (" + first + ")", 3f);

            int slot = input.Bindings.FindRow("crouch").Primary(ControlColumn.KeyboardMouse);
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
            yield return Keys(Key.LeftCtrl);
            Assert.That(input.Crouch.IsPressed(), Is.True, "Left Ctrl crouches before the rebind");
            yield return Keys();

            // Select Crouch's keyboard cell and press L.
            Assert.That(ui.Controls.Activate(ControlsScreen.SlotButton("crouch", ControlColumn.KeyboardMouse)), Is.True);
            Assert.That(ui.Controls.IsListening, Is.True, "waiting for a key");
            yield return null;
            _keyboard.Set(Key.L);
            yield return WaitFor(() => !ui.Controls.IsListening, "the rebind to finish", 5f);
            Assert.That(ui.Controls.IsAskingAboutConflict, Is.False, "L isn't used by anything else");
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/l"));
            Assert.That(ui.Controls.CellText("crouch", ControlColumn.KeyboardMouse), Does.StartWith("L"));
            Assert.That(GameRoot.Save.Data.GetEntry(ControlBindings.SaveKey), Is.Not.Null.And.Not.Empty, "saved at once");
            Assert.That(ui.IsOverlayOpen(ui.Controls), Is.True, "the screen stays open");
            yield return Keys();

            yield return Keys(Key.L);
            Assert.That(input.Crouch.IsPressed(), Is.True, "L crouches now");
            yield return Keys();
            Assert.That(input.Crouch.IsPressed(), Is.False);
            yield return Keys(Key.LeftCtrl);
            Assert.That(input.Crouch.IsPressed(), Is.False, "Left Ctrl doesn't any more");
            yield return Keys();

            // A fresh load of the same save brings the rebind back (what the next launch does).
            input.Bindings.ResetAll();
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
            input.Bindings.LoadFrom(GameRoot.Save.Data);
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/l"));

            // The row's Reset.
            Assert.That(ui.Controls.Activate(ControlsScreen.ResetButton("crouch")), Is.True);
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
            Assert.That(GameRoot.Save.Data.GetEntry(ControlBindings.SaveKey), Is.Null, "back to defaults: nothing saved");
            yield return Keys(Key.LeftCtrl);
            Assert.That(input.Crouch.IsPressed(), Is.True, "Left Ctrl crouches again");
            yield return Keys();

            // Back closes the screen and returns to Settings.
            ui.Back();
            Assert.That(ui.IsOverlayOpen(ui.Controls), Is.False);
            Assert.That(ui.Settings.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapeCancelsAndAConflictCanSwap()
        {
            var ui = GameRoot.Ui;
            var input = GameRoot.Input;
            ui.OpenSettings();
            ui.Settings.Activate("controls");
            yield return null;
            int slot = input.Bindings.FindRow("crouch").Primary(ControlColumn.KeyboardMouse);

            // Escape cancels and leaves the binding (and the screen) alone.
            Assert.That(ui.Controls.BeginRebind("crouch", ControlColumn.KeyboardMouse), Is.True);
            yield return null;
            _keyboard.Set(Key.Escape);
            yield return WaitFor(() => !ui.Controls.IsListening, "Escape to cancel", 5f);
            yield return Keys();
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
            Assert.That(ui.IsOverlayOpen(ui.Controls), Is.True, "Escape cancelled the rebind, not the screen");

            // E is Interact's: the screen asks, Swap gives Interact Left Ctrl.
            yield return WaitFor(() => ui.Controls.BeginRebind("crouch", ControlColumn.KeyboardMouse), "a new rebind", 2f);
            yield return null;
            _keyboard.Set(Key.E);
            yield return WaitFor(() => ui.Controls.IsAskingAboutConflict, "the conflict question", 5f);
            Assert.That(ui.Controls.PromptText, Does.Contain("Interact"));
            Assert.That(ui.Controls.Activate(ControlsScreen.SwapButton), Is.True);
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/e"));
            int interact = input.Bindings.FindRow("interact").Primary(ControlColumn.KeyboardMouse);
            Assert.That(input.Interact.bindings[interact].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"), "swapped");
            Assert.That(ui.Controls.IsAskingAboutConflict || ui.Controls.IsListening, Is.False);
            yield return Keys();

            // Reset all.
            Assert.That(ui.Controls.Activate(ControlsScreen.ResetAllButton), Is.True);
            Assert.That(input.Bindings.IsDefault, Is.True);
        }
    }
}

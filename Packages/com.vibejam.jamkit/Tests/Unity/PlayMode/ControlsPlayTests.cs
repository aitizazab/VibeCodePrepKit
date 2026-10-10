using System.Collections;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.Play
{
    /// <summary>Settings > Controls in the running shell: a real interactive rebind of Crouch on a test
    /// keyboard, then Crouch answers the new key and not the old one, the rebind is saved, Reset undoes it.</summary>
    public class ControlsPlayTests
    {
        Keyboard _keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _keyboard = InputSystem.AddDevice<Keyboard>("ControlsTestKeyboard");
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (!GameRoot.Exists) return;
            GameRoot.Ui.CloseOverlays();
            GameRoot.Input.Bindings.ResetAll();
        }

        IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
        }

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
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.L));
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
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            yield return WaitFor(() => !ui.Controls.IsListening, "Escape to cancel", 5f);
            yield return Keys();
            Assert.That(input.Crouch.bindings[slot].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
            Assert.That(ui.IsOverlayOpen(ui.Controls), Is.True, "Escape cancelled the rebind, not the screen");

            // E is Interact's: the screen asks, Swap gives Interact Left Ctrl.
            yield return WaitFor(() => ui.Controls.BeginRebind("crouch", ControlColumn.KeyboardMouse), "a new rebind", 2f);
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
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

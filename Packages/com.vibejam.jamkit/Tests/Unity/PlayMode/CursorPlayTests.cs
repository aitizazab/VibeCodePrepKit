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
    /// <summary>The cursor follows the device in use, in the running shell (on the title): a gamepad hides it (and turns
    /// the menus' hover looks off), a real mouse move shows it again, a nudge of a pixel or two doesn't, and a cursor
    /// a scene locked for mouse-look is left locked. Cursor.visible itself is only read back outside batch mode (a
    /// batch run has no window cursor); the shell's own state is checked everywhere.</summary>
    public class CursorPlayTests
    {
        TestInput _input;
        Gamepad _pad;
        Mouse _mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _input = new TestInput("CursorTestKeyboard");
            _pad = _input.AddGamepad("CursorTestPad");
            _mouse = InputSystem.AddDevice<Mouse>("CursorTestMouse");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameRoot.Input.SetPadMode(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            _input?.Dispose();
            _input = null;
            Cursor.lockState = CursorLockMode.None;
            if (GameRoot.Exists) GameRoot.Input.SetPadMode(false);
            yield return null;
            Cursor.visible = true;
        }

        static bool RootHasPadClass => GameRoot.Ui.Root.ClassListContains(PointerCursor.PadClass);

        IEnumerator PushRightStick()
        {
            // The right stick: nothing on the title answers it, so the screen stays put.
            InputSystem.QueueStateEvent(_pad, new GamepadState { rightStick = new Vector2(0.9f, 0f) });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        IEnumerator MoveMouse(float pixels)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(600f + pixels, 400f), delta = new Vector2(pixels, 0f) });
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePadHidesTheCursorAndTheMouseBringsItBack()
        {
            Assert.That(GameRoot.Input.PadMode, Is.False);
            Assert.That(PointerCursor.HiddenForPad, Is.False);

            yield return PushRightStick();
            Assert.That(GameRoot.Input.PadMode, Is.True, "a gamepad was used");
            Assert.That(GameRoot.Input.HoverAllowed, Is.False, "the hidden cursor's hover mustn't take focus");
            Assert.That(PointerCursor.HiddenForPad, Is.True, "the cursor is hidden for the pad");
            Assert.That(RootHasPadClass, Is.True, "the menus' hover looks are off (" + PointerCursor.PadClass + ")");
            if (!Application.isBatchMode) Assert.That(Cursor.visible, Is.False, "Cursor.visible");

            yield return MoveMouse(1f);
            Assert.That(GameRoot.Input.PadMode, Is.True, "a one-pixel nudge isn't the mouse being used");

            yield return MoveMouse(40f);
            Assert.That(GameRoot.Input.PadMode, Is.False, "the mouse moved");
            Assert.That(PointerCursor.HiddenForPad, Is.False, "the cursor is back");
            Assert.That(RootHasPadClass, Is.False, "hover looks are back");
            if (!Application.isBatchMode) Assert.That(Cursor.visible, Is.True, "Cursor.visible");

            // A button press counts as well as a stick.
            _input.SetPad(GamepadButton.DpadDown);
            yield return null;
            yield return null;
            _input.SetPad();
            yield return null;
            Assert.That(GameRoot.Input.PadMode, Is.True, "the d-pad");
        }

        [UnityTest]
        public IEnumerator ALockedCursorIsLeftToTheScene()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return null;
            bool lockReadsBack = Cursor.lockState == CursorLockMode.Locked; // a batch run has no window to lock to
            yield return PushRightStick();
            Assert.That(GameRoot.Input.PadMode, Is.True);
            if (lockReadsBack)
            {
                Assert.That(PointerCursor.HiddenForPad, Is.False, "a locked cursor belongs to the scene's mouse-look");
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "still locked");
            }
            yield return MoveMouse(40f);
            Assert.That(GameRoot.Input.PadMode, Is.False);
            if (lockReadsBack)
            {
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "the mouse doesn't unlock it either");
                Assert.That(PointerCursor.HiddenForPad, Is.False);
            }
        }
    }
}

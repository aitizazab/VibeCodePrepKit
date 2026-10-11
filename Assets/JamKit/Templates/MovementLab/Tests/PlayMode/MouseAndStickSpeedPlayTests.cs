using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Tests.MovementLabSpeed
{
    /// <summary>The keyboard-and-mouse exploits the creator found (2026-10-12), driven through real input devices on the
    /// real controller, and the same standard for a gamepad: (a) sprinting while sweeping the view (the mouse at slow,
    /// fast and flick rates; the right stick at full tilt with its turn boost) never pushes the speed past sprint; (b) a
    /// chain of hops from 19 m/s doesn't keep that speed for free (ten hops bleed at least 1 m/s). They run on a big flat
    /// floor the test puts in the sky, out of the course's way.</summary>
    public class MouseAndStickSpeedPlayTests
    {
        TestInput _input;
        Mouse _mouse;
        GameObject _floor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _input = new TestInput("SpeedTestKeyboard");
            _mouse = InputSystem.AddDevice<Mouse>("SpeedTestMouse");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _input?.Dispose();
            _input = null;
            if (_floor != null) Object.Destroy(_floor);
            DebugMenu.TimeScale = 1f;
            PresenterMode.Close();
            yield return null;
        }

        static MovementController Body => MovementLab.MovementLabGame.Current.player;

        IEnumerator OnTheTestFloor(float speed)
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLab.MovementLabPresenter.Scene), Is.True);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLab.MovementLabPresenter.Scene
                && MovementLab.MovementLabGame.Current != null && Body.Sim != null, "the Movement Lab");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the fade-in", 5f);
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Speed test floor";
            _floor.transform.position = new Vector3(4000f, 199.5f, 4000f);
            _floor.transform.localScale = new Vector3(800f, 1f, 800f);
            Physics.SyncTransforms();
            Body.Teleport(new Vector3(4000f, 200.02f, 3700f), 0f, new Vector3(0f, 0f, speed));
            yield return null;
        }

        void Mouse(float dx) => InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(dx, 0f) });

        [UnityTest]
        public IEnumerator SprintingWhileSweepingTheMouseNeverPassesSprint()
        {
            yield return OnTheTestFloor(9.5f);
            float sprint = Body.Def.sprintSpeed, top = 0f;
            // W + Shift held; the mouse sweeps at several rates (pixels a frame), then flicks.
            foreach (float px in new[] { 4f, 12f, 30f, 80f })
            {
                _input.Set(Key.W, Key.LeftShift);
                for (int f = 0; f < 150; f++)
                {
                    Mouse(px);
                    yield return null;
                    top = Mathf.Max(top, Body.Speed);
                }
            }
            for (int f = 0; f < 120; f++)
            {
                Mouse(f % 6 == 0 ? (f % 12 == 0 ? 900f : -900f) : 0f); // flicks back and forth
                yield return null;
                top = Mathf.Max(top, Body.Speed);
            }
            _input.Set();
            TestContext.Out.WriteLine($"keyboard and mouse: top speed {top:0.00} m/s while sweeping (sprint {sprint})");
            Assert.That(top, Is.LessThanOrEqualTo(sprint + 0.05f), "the mouse sweep exploit (was up to 22 m/s)");
        }

        [UnityTest]
        public IEnumerator SprintingWhileSweepingTheRightStickNeverPassesSprint()
        {
            yield return OnTheTestFloor(9.5f);
            var pad = _input.AddGamepad("SpeedTestPad");
            float sprint = Body.Def.sprintSpeed, top = 0f;
            var state = new GamepadState { leftStick = new Vector2(0f, 1f), rightStick = new Vector2(1f, 0f) }.WithButton(GamepadButton.LeftShoulder);
            InputSystem.QueueStateEvent(pad, state);
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                yield return null;
                top = Mathf.Max(top, Body.Speed);
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            TestContext.Out.WriteLine($"gamepad: top speed {top:0.00} m/s with the right stick at full tilt (sprint {sprint}; the boosted 304 deg/s turn used to reach 19.9)");
            Assert.That(top, Is.LessThanOrEqualTo(sprint + 0.05f));
        }

        [UnityTest]
        public IEnumerator TenHopsFrom19MetresASecondBleedAtLeastOne()
        {
            yield return OnTheTestFloor(19f);
            int hops = 0;
            bool wasGrounded = Body.Sim.Grounded, held = false;
            float until = Time.time + 25f;
            while (hops < 10 && Time.time < until)
            {
                // W held; Space pressed on each landing (released in the air), the mouse sweeping a little.
                bool grounded = Body.Sim.Grounded;
                bool press = grounded && !held;
                held = press;
                if (press) _input.Set(Key.W, Key.Space); else _input.Set(Key.W);
                Mouse(grounded ? 0f : 6f);
                yield return null;
                if (Body.FrameEvents.jumped) hops++;
                wasGrounded = grounded;
            }
            _input.Set(Key.W);
            yield return WaitFor(() => Body.Sim.Grounded, "the last landing", 3f);
            float end = Body.Speed;
            _input.Set();
            TestContext.Out.WriteLine($"ten hops from 19 m/s on keyboard: {end:0.00} m/s ({hops} hops)");
            Assert.That(hops, Is.EqualTo(10));
            Assert.That(end, Is.LessThanOrEqualTo(18f), "a hop chain doesn't keep its speed for free");
        }
    }
}

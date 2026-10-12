using System.Collections;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The waking world's controls through a test gamepad and keyboard (the creator, 2026-10-12: "I couldn't run
    /// normally, I had to use the Left Bumper"): she hurries the way the dream sprints, with L3 (a click: until she stops
    /// walking forward) or Shift, and LB no longer hurries; the bumpers peek instead (RB right, LB left), slowly, a wall stops
    /// the lean short of it, and hurrying lets it go. Day 1 on a floor the test puts in the sky, out of the ward's way.</summary>
    public class VisitingHoursControlsPlayTests
    {
        TestInput _input;
        Gamepad _pad;
        GameObject _floor, _wall;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _input = new TestInput("DayControlsKeyboard");
            _pad = _input.AddGamepad("DayControlsPad");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pad != null && _pad.added) InputSystem.QueueStateEvent(_pad, new GamepadState());
            if (_input != null) _input.Set();
            yield return null;
            _input?.Dispose();
            _input = null;
            if (_floor != null) Object.Destroy(_floor);
            if (_wall != null) Object.Destroy(_wall);
            VisitingHoursGame.FastTransitions = false;
            yield return null;
        }

        static VisitingHoursGame Game => VisitingHoursGame.Current;

        IEnumerator DayOnTheSkyFloor()
        {
            Assert.That(GameRoot.Input.Bindings.PeekOnBumpers, Is.True, "Visiting Hours turns peeking on at boot (VhBoot)");
            VisitingHoursGame.FastTransitions = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            Assert.That(GameRoot.Instance.SceneService.Load("VisitingHours"), Is.True, "VisitingHours is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "VisitingHours"
                && Game != null && Game.CurrentMode == VisitingHoursGame.Mode.Explore, "Day 1");
            yield return WaitFor(() => GameRoot.Dialogue.IsActive, "the Day 1 intro");
            yield return FinishDialogue();
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Day controls test floor";
            _floor.transform.position = new Vector3(3000f, 199.5f, 3000f);
            _floor.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();
            Game.player.Teleport(new Vector3(3000f, 200.05f, 3000f));
            Game.player.FaceYaw(0f);
            yield return Hold(0.3f);
            Assert.That(Game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.Explore));
        }

        void Pad(Vector2 stick, params GamepadButton[] buttons)
        {
            var state = new GamepadState { leftStick = stick };
            foreach (var b in buttons) state = state.WithButton(b);
            InputSystem.QueueStateEvent(_pad, state);
        }

        static IEnumerator Hold(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        /// <summary>The top speed over <paramref name="seconds"/>, and whether she hurried at all.</summary>
        IEnumerator Top(float seconds, float[] top, bool[] hurried)
        {
            top[0] = 0f;
            hurried[0] = false;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
                top[0] = Mathf.Max(top[0], Game.player.Speed);
                hurried[0] |= Game.Hurrying;
            }
        }

        [UnityTest]
        public IEnumerator SheHurriesOnL3AndShiftLikeTheDreamAndNotOnLB()
        {
            yield return DayOnTheSkyFloor();
            var motion = Game.config.player.motion;
            var top = new float[1];
            var hurried = new bool[1];

            // The stick alone: a walk.
            Pad(new Vector2(0f, 1f));
            yield return Top(1f, top, hurried);
            Assert.That(hurried[0], Is.False);
            Assert.That(top[0], Is.LessThanOrEqualTo(motion.walkSpeed + 0.1f), "the stick alone walks");

            // LB held: no longer a hurry (it's the peek now).
            Pad(new Vector2(0f, 1f), GamepadButton.LeftShoulder);
            yield return Top(1f, top, hurried);
            Assert.That(hurried[0], Is.False, "LB doesn't hurry any more");
            Assert.That(top[0], Is.LessThanOrEqualTo(motion.walkSpeed + 0.1f));

            // A click of L3 with the stick forward: she hurries, and keeps hurrying with L3 let go.
            Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
            yield return Frames(3);
            Pad(new Vector2(0f, 1f));
            yield return Top(1.2f, top, hurried);
            TestContext.Out.WriteLine($"L3: top {top[0]:0.00} m/s (walk {motion.walkSpeed}, hurry {motion.runSpeed})");
            Assert.That(Game.Hurrying, Is.True, "the click keeps her hurrying (the dream's L3 toggle)");
            Assert.That(top[0], Is.GreaterThan(motion.runSpeed - 0.3f), "at the hurry speed");
            // Letting the stick go ends it.
            Pad(Vector2.zero);
            yield return Frames(3);
            Assert.That(Game.Hurrying, Is.False, "stopping walking forward ends the L3 hurry");
            yield return Hold(0.5f);

            // The keyboard: W + Left Shift.
            _input.Set(Key.W, Key.LeftShift);
            yield return Top(1f, top, hurried);
            Assert.That(hurried[0], Is.True, "Shift hurries");
            Assert.That(top[0], Is.GreaterThan(motion.runSpeed - 0.3f));
            _input.Set();
            yield return Hold(0.3f);
        }

        [UnityTest]
        public IEnumerator TheBumpersPeekAndAWallStopsIt()
        {
            yield return DayOnTheSkyFloor();
            var peek = Game.peek;
            var cam = Game.cam.transform;
            var p = Game.player;

            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(0.4f);
            float sideways = Vector3.Dot(cam.position - p.EyePosition, p.transform.right);
            TestContext.Out.WriteLine($"RB: peek {Game.PeekOffset:0.000} m, camera {sideways:0.000} m sideways");
            Assert.That(Game.PeekOffset, Is.EqualTo(peek.distance).Within(0.01f), "RB: a full lean right");
            Assert.That(sideways, Is.EqualTo(peek.distance).Within(0.05f), "the camera went with it");
            var comfort = GameRoot.Settings;
            float rollK = comfort.reduceEffects ? 0f : comfort.cameraRoll * comfort.motionEffects;
            float roll = Mathf.Asin(Mathf.Clamp(Vector3.Dot(cam.up, p.transform.right), -1f, 1f)) * Mathf.Rad2Deg;
            Assert.That(roll, Is.EqualTo(peek.roll * rollK).Within(0.3f), "the view rolls a few degrees toward the lean (x the comfort settings)");

            Pad(Vector2.zero, GamepadButton.LeftShoulder);
            yield return Hold(0.5f);
            Assert.That(Game.PeekOffset, Is.EqualTo(-peek.distance).Within(0.01f), "LB: a full lean left");

            // Q / F on the keyboard.
            Pad(Vector2.zero);
            _input.Set(Key.F);
            yield return Hold(0.5f);
            Assert.That(Game.PeekOffset, Is.EqualTo(peek.distance).Within(0.01f), "F: right");
            _input.Set(Key.Q);
            yield return Hold(0.5f);
            Assert.That(Game.PeekOffset, Is.EqualTo(-peek.distance).Within(0.01f), "Q: left");
            _input.Set();
            yield return Hold(0.4f);
            Assert.That(Game.PeekOffset, Is.EqualTo(0f).Within(1e-3f), "let go: upright");

            // A wall to her right, its face 0.45 m from her middle: the lean stops short of it.
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Day peek test wall";
            _wall.transform.position = p.transform.position + p.transform.right * 0.55f + Vector3.up * 1.5f;
            _wall.transform.rotation = p.transform.rotation;
            _wall.transform.localScale = new Vector3(0.2f, 3f, 2f);
            Physics.SyncTransforms();
            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(0.5f);
            sideways = Vector3.Dot(cam.position - p.EyePosition, p.transform.right);
            Assert.That(Game.PeekOffset, Is.GreaterThan(0.1f).And.LessThanOrEqualTo(0.45f - peek.clearance + 0.01f), "stopped short of the wall");
            Assert.That(sideways, Is.LessThan(0.45f - 0.1f), "the camera stays out of the wall");
            Object.Destroy(_wall);
            _wall = null;
            yield return Hold(0.4f);
            Assert.That(Game.PeekOffset, Is.GreaterThan(peek.distance - 0.01f), "the wall gone: the full lean");

            // Hurrying (L3 with the stick forward) lets it go, RB still held.
            Pad(new Vector2(0f, 1f), GamepadButton.RightShoulder, GamepadButton.LeftStick);
            yield return Frames(3);
            Pad(new Vector2(0f, 1f), GamepadButton.RightShoulder);
            yield return Hold(0.4f);
            Assert.That(Game.Hurrying, Is.True);
            Assert.That(Game.PeekOffset, Is.EqualTo(0f).Within(0.01f), "hurrying cancels the peek");
            Pad(Vector2.zero);
            yield return Hold(0.3f);
        }
    }
}

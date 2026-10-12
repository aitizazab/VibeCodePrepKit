using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Peeking in the dream, on Night 2's real controller, driven through a test gamepad (docs/MOVEMENT.md
    /// "Peeking"): RB leans the eyes right and LB left (the view rolls a few degrees with it, the first-person body bends
    /// with it), a wall stops the lean short of it, a sprint (L3) or a jump lets it go; Theo's sight takes her head where the
    /// lean put it, so peeking round one of the house's door frames shows him far less of her than stepping out; Theo's first
    /// count teaches it. The movement checks run on a floor the test puts in the sky, out of the house's way.</summary>
    public class DreamPeekPlayTests
    {
        TestInput _input;
        Gamepad _pad;
        GameObject _floor, _wall;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _input = new TestInput("PeekTestKeyboard");
            _pad = _input.AddGamepad("PeekTestPad");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pad != null && _pad.added) InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            _input?.Dispose();
            _input = null;
            if (_floor != null) Object.Destroy(_floor);
            if (_wall != null) Object.Destroy(_wall);
            DebugMenu.TimeScale = 1f;
            PresenterMode.Close();
            yield return null;
        }

        static DreamHouseGame Game => DreamHouseGame.Current;
        static MovementController Body => Game.player;

        static IEnumerator HideAndSeek()
        {
            Assert.That(GameRoot.Input.Bindings.PeekOnBumpers, Is.True, "Visiting Hours turns peeking on at boot (VhBoot)");
            Assert.That(GameRoot.Instance.SceneService.Load(DreamHousePresenter.Scene), Is.True, "DreamHouse is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamHousePresenter.Scene
                && Game != null && Game.House != null, "the house");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
            Assert.That(PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterHideAndSeek), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Phase, Is.EqualTo(HousePhase.HideAndSeek));
        }

        IEnumerator OnTheSkyFloor()
        {
            yield return HideAndSeek();
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Peek test floor";
            _floor.transform.position = new Vector3(5000f, 199.5f, 5000f);
            _floor.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();
            Body.Teleport(new Vector3(5000f, 200.02f, 5000f), 0f, Vector3.zero);
            yield return Hold(0.3f);
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

        /// <summary>The camera's sideways offset from the body's centre line (m, + right).</summary>
        static float CameraSideways()
        {
            var cam = Body.cam.transform;
            Vector3 right = Quaternion.Euler(0f, Body.Yaw, 0f) * Vector3.right;
            return Vector3.Dot(cam.position - Body.RenderPosition, right);
        }

        [UnityTest]
        public IEnumerator BumpersLeanTheEyesRollTheViewAndBendTheBody()
        {
            yield return OnTheSkyFloor();
            var peek = Body.peek;
            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(0.4f);
            TestContext.Out.WriteLine($"RB: lean {Body.LeanOffset:0.000} m, camera {CameraSideways():0.000} m sideways");
            Assert.That(Body.LeanOffset, Is.EqualTo(peek.distance).Within(0.01f), "RB: a full lean right");
            Assert.That(CameraSideways(), Is.EqualTo(peek.distance).Within(0.06f), "the camera went with it");
            var feel = Body.cam.GetComponentInParent<MovementCamera>();
            var comfort = GameRoot.Settings;
            float rollK = comfort.reduceEffects ? 0f : comfort.cameraRoll * comfort.motionEffects;
            if (feel != null)
            {
                Assert.That(feel.LeanRoll, Is.EqualTo(-peek.roll * rollK).Within(0.05f), "a few degrees of roll toward the lean, x the comfort settings");
                Assert.That(Mathf.Abs(feel.LeanRoll), Is.LessThanOrEqualTo(5f), "never more than a few degrees");
            }
            var fpBody = Object.FindAnyObjectByType<MovementBody>();
            if (fpBody != null && fpBody.UsingRig)
            {
                Vector3 right = Quaternion.Euler(0f, Body.Yaw, 0f) * Vector3.right;
                float head = Vector3.Dot(fpBody.rig.HeadPosition - Body.RenderPosition, right);
                TestContext.Out.WriteLine($"her body's head {head:0.000} m to the right");
                Assert.That(head, Is.GreaterThan(peek.distance * 0.5f), "the body leans with the eyes");
            }

            Pad(Vector2.zero, GamepadButton.LeftShoulder);
            yield return Hold(0.5f);
            Assert.That(Body.LeanOffset, Is.EqualTo(-peek.distance).Within(0.01f), "LB: a full lean left");
            Assert.That(CameraSideways(), Is.LessThan(-peek.distance + 0.06f));

            Pad(Vector2.zero);
            yield return Hold(0.4f);
            Assert.That(Body.LeanOffset, Is.EqualTo(0f).Within(1e-3f), "let go: upright");

            // Walking while leaning is slow.
            Pad(new Vector2(0f, 1f), GamepadButton.RightShoulder);
            yield return Hold(0.8f);
            Assert.That(Body.Speed, Is.LessThanOrEqualTo(Body.Def.walkSpeed * peek.walkShare + 0.2f), "leaning, she only walks slowly");
            Assert.That(Body.LeanOffset, Is.GreaterThan(peek.distance * 0.9f), "and still leans");
            Pad(Vector2.zero);
            yield return Hold(0.3f);
        }

        [UnityTest]
        public IEnumerator AWallStopsTheLeanShortOfIt()
        {
            yield return OnTheSkyFloor();
            // A wall to her right, its face 0.45 m from her middle.
            Vector3 feet = Body.RenderPosition;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Peek test wall";
            _wall.transform.position = feet + new Vector3(0.55f, 1.5f, 0f);
            _wall.transform.localScale = new Vector3(0.2f, 3f, 2f);
            Physics.SyncTransforms();
            yield return null;
            float top = 0f;
            Pad(Vector2.zero, GamepadButton.RightShoulder);
            float until = Time.time + 0.6f;
            while (Time.time < until) { yield return null; top = Mathf.Max(top, CameraSideways()); }
            float face = 0.45f;
            TestContext.Out.WriteLine($"against the wall: lean {Body.LeanOffset:0.000} m, camera at most {top:0.000} m (the face at {face})");
            Assert.That(Body.LeanOffset, Is.GreaterThan(0.1f), "she still leans as far as there's room");
            Assert.That(Body.LeanOffset, Is.LessThanOrEqualTo(face - Body.peek.clearance + 0.01f), "stopped short of the wall");
            Assert.That(top, Is.LessThan(face - 0.1f), "the camera never got near the wall, let alone through it");
            Pad(Vector2.zero);
            yield return Hold(0.3f);
        }

        [UnityTest]
        public IEnumerator SprintingOrJumpingLetsTheLeanGo()
        {
            yield return OnTheSkyFloor();
            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(0.4f);
            Assert.That(Body.LeanOffset, Is.GreaterThan(0.35f));
            // L3 with the stick forward (RB still held): she sprints and the lean goes.
            Pad(new Vector2(0f, 1f), GamepadButton.RightShoulder, GamepadButton.LeftStick);
            yield return Frames(3);
            Pad(new Vector2(0f, 1f), GamepadButton.RightShoulder);
            yield return Hold(0.5f);
            TestContext.Out.WriteLine($"sprinting: {Body.Mode}, {Body.Speed:0.0} m/s, lean {Body.LeanOffset:0.000}");
            Assert.That(Body.Mode, Is.EqualTo(MoveMode.Sprint), "L3 sprints (as the day now does)");
            Assert.That(Body.LeanOffset, Is.EqualTo(0f).Within(0.01f), "sprinting cancels the peek");

            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(1f);
            Assert.That(Body.LeanOffset, Is.GreaterThan(0.35f), "stopped: leaning again");
            // A (jump), RB held: in the air the lean goes.
            Pad(Vector2.zero, GamepadButton.RightShoulder, GamepadButton.South);
            float low = 1f;
            float until = Time.time + 0.45f;
            while (Time.time < until) { yield return null; low = Mathf.Min(low, Mathf.Abs(Body.LeanOffset)); }
            Assert.That(low, Is.LessThan(0.02f), "jumping cancels the peek");
            Pad(Vector2.zero);
            yield return Hold(0.8f);
        }

        // ---------- Theo's sight ----------

        static NVec3 At(HouseDoor d, float along, float across, float y)
            => d.alongX ? new NVec3(along, y, across) : new NVec3(across, y, along);

        static float YawTo(NVec3 from, NVec3 to) => Mathf.Atan2(to.X - from.X, to.Z - from.Z) * Mathf.Rad2Deg;

        static float TheoSees(NVec3 theoFeet, NVec3 herFeet, in StealthTarget her)
        {
            Game.Theo.Place(theoFeet, YawTo(theoFeet, herFeet));
            return Game.Theo.VisibilityOf(her);
        }

        static StealthTarget Standing(NVec3 feet, NVec3 lean)
        {
            var t = StealthTarget.At(feet);
            t.lean = lean;
            return t;
        }

        /// <summary>A door frame to peek round (as the core's HousePeekTests finds them), where her body also fits in the real
        /// scene and there's room to lean: her feet behind the jamb, stepped out, Theo's feet, her facing and her lean side.</summary>
        static bool FindDoorFrame(out NVec3 behind, out NVec3 stepped, out NVec3 theo, out float yaw, out int side)
        {
            behind = stepped = theo = default;
            yaw = 0f;
            side = 0;
            const int mask = ~(1 << 2);
            foreach (var d in Game.House.doors)
            {
                if (d.exterior || d.y1 - d.y0 < 1.9f || d.b - d.a < 0.75f) continue;
                for (int s = -1; s <= 1; s += 2)
                    for (int j = 0; j < 2; j++)
                    {
                        float jamb = j == 0 ? d.a : d.b, dir = j == 0 ? 1f : -1f;
                        var b = At(d, jamb - dir * 0.45f, d.c - s * 0.6f, d.y0);
                        var st = At(d, jamb + dir * 0.45f, d.c - s * 0.6f, d.y0);
                        var toward = At(d, dir, 0f, 0f);
                        var facing = At(d, 0f, s, 0f);
                        // In the scene: she fits there, stands on something, and has room to lean toward the opening.
                        Vector3 feet = Game.ToWorld(b);
                        Vector3 towardW = Game.houseRoot.TransformDirection(new Vector3(toward.X, 0f, toward.Z));
                        if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.3f, 0.36f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, 0.6f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.SphereCast(feet + Vector3.up * 1.5f, 0.2f, towardW, out _, 0.42f, mask, QueryTriggerInteraction.Ignore)) continue;
                        foreach (float depth in new[] { 3f, 2.5f, 3.5f, 4f })
                            foreach (float off in new[] { 0.7f, 0.45f, 0.95f })
                            {
                                var t = At(d, jamb + dir * off, d.c + s * depth, d.y0);
                                if (TheoSees(t, b, Standing(b, default)) > 0f) continue;
                                if (TheoSees(t, st, Standing(st, default)) < 0.5f) continue;
                                if (TheoSees(t, b, Standing(b, toward * 0.4f)) <= 0f) continue;
                                behind = b; stepped = st; theo = t;
                                float yawHouse = Mathf.Atan2(facing.X, facing.Z) * Mathf.Rad2Deg;
                                yaw = yawHouse + Game.houseRoot.eulerAngles.y;
                                // Her right in the house is the facing turned 90 degrees clockwise (x, z) -> (z, -x).
                                side = toward.X * facing.Z - toward.Z * facing.X > 0f ? 1 : -1;
                                TestContext.Out.WriteLine($"peeking round the {d.name}: Theo {depth} m out on the far side");
                                return true;
                            }
                    }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator PeekingRoundADoorFrameShowsTheoFarLessThanSteppingOut()
        {
            yield return HideAndSeek();
            Game.Theo.Stop(); // he stands where the test puts him (his senses still work: VisibilityOf)
            Assert.That(FindDoorFrame(out var behind, out var stepped, out var theoAt, out float yaw, out int side), Is.True,
                "a door frame in the house to peek round");
            Body.Teleport(Game.ToWorld(behind), yaw, Vector3.zero);
            yield return Hold(0.3f);

            // Upright behind the jamb: unseen.
            float hidden = TheoSees(theoAt, behind, Game.HerTarget());
            // Holding the bumper toward the opening: her head goes round the frame.
            Pad(Vector2.zero, side > 0 ? GamepadButton.RightShoulder : GamepadButton.LeftShoulder);
            yield return Hold(0.4f);
            var her = Game.HerTarget();
            float lean = new Vector2(her.lean.X, her.lean.Z).magnitude;
            float peek = TheoSees(theoAt, behind, her);
            Pad(Vector2.zero);
            yield return Hold(0.3f);
            // Stepping out into the doorway instead.
            Body.Teleport(Game.ToWorld(stepped), yaw, Vector3.zero);
            yield return Hold(0.3f);
            float out_ = TheoSees(theoAt, stepped, Game.HerTarget());

            var def = Game.Theo.Definition;
            TestContext.Out.WriteLine($"Theo sees: behind the frame {hidden:0.000}, peeking ({lean:0.00} m) {peek:0.000}, stepped out {out_:0.000}; "
                + $"his meter fills in {(peek > 0f ? 1f / (peek * def.fillRate) : 0f):0.0} s peeking against {1f / (out_ * def.fillRate):0.0} s stepped out");
            Assert.That(lean, Is.GreaterThan(0.3f), "Theo's senses get her head where the lean put it");
            Assert.That(hidden, Is.EqualTo(0f), "upright behind the frame: unseen");
            Assert.That(peek, Is.GreaterThan(0f), "peeking shows him her head: not free");
            Assert.That(peek, Is.LessThanOrEqualTo(out_ * 0.3f), "a small silhouette against all of her");
        }

        [UnityTest]
        public IEnumerator TheosFirstCountTeachesThePeek()
        {
            yield return HideAndSeek();
            Assert.That(Game.Theo.Counting, Is.True);
            GameRoot.Input.SetPadMode(true);
            yield return Frames(2);
            Assert.That(Game.PeekHintText, Is.Not.Null, "the hint is up while he counts");
            Assert.That(Game.Hud.PromptText, Does.Contain("Hold to peek round corners"));
            Assert.That(Game.Hud.PromptText, Does.Contain("LB / RB"), "in the pad's buttons");
            Pad(Vector2.zero, GamepadButton.RightShoulder);
            yield return Hold(0.4f);
            Pad(Vector2.zero);
            yield return Frames(2);
            Assert.That(Game.PeekHintText, Is.Null, "she's peeked: it goes");
            GameRoot.Input.SetPadMode(false);
        }
    }
}

using JamKit.Tests.Play;
using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The Movement Lab in its real scene, on Unity's CharacterController: the autopilot's
    /// scripted chain (slide, slide jump, wallrun, wall kick, double jump) keeps its speed; the autopilot
    /// finishes the whole course; standing still in the chase gets you caught and put back; falling
    /// respawns you; every presenter chapter jumps cleanly; the comfort settings reach the camera.
    /// Speeds are written to the test output.</summary>
    public class MovementLabPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.SetAutopilot(false);
            PresenterMode.Close();
            if (GameRoot.Exists)
            {
                var s = GameRoot.Settings;
                s.fieldOfView = 68f; s.cameraRoll = 1f; s.headBob = 1f; s.motionEffects = 1f; s.speedLines = true;
            }
            yield return null;
        }

        static MovementLabGame Game => MovementLabGame.Current;

        /// <summary>Waits until a respawn has really put the player back and the fade is gone.</summary>
        static IEnumerator RespawnDone() => WaitFor(() => !Game.Respawning && GameRoot.Ui.FadeAlpha == 0f, "the respawn to finish", 5f);

        static void PrintTrace(string title)
        {
            TestContext.Out.WriteLine(title);
            TestContext.Out.WriteLine(Game.TraceText);
        }

        static IEnumerator StartLab()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True, "MovementLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && Game != null && Game.Course != null, "the Movement Lab scene");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheScriptedChainKeepsItsSpeed()
        {
            yield return StartLab();
            Game.JumpToChapter(LabCourse.ChapterChain);
            yield return Frames(2);
            var body = Game.player;
            int respawns = Game.Respawns;
            Game.SetAutopilot(true);
            float slideJump = 0f, wallEntry = 0f, afterKick = 0f, afterDouble = 0f, top = 0f;
            bool slid = false, kick = false;
            float end = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < end && !(Game.S >= 236f && body.Sim.Grounded))
            {
                var ev = body.FrameEvents;
                float v = body.Speed;
                top = Mathf.Max(top, v);
                if (ev.slideStarted) slid = true;
                if (ev.jumped && slid && slideJump == 0f) slideJump = v;
                if (ev.wallrunStarted && wallEntry == 0f) wallEntry = v;
                if (ev.wallJumped && afterKick == 0f) { afterKick = v; kick = ev.wallKick; }
                if (ev.doubleJumped && afterDouble == 0f) afterDouble = v;
                yield return null;
            }
            Game.SetAutopilot(false);
            PrintTrace("Trace (state changes and respawns):");
            TestContext.Out.WriteLine($"slide jump {slideJump:0.00}, wall entry {wallEntry:0.00}, after the kick {afterKick:0.00} (wall kick {kick}), after the double jump {afterDouble:0.00}, top {top:0.00} m/s");
            Assert.That(Game.Respawns, Is.EqualTo(respawns), "no fall");
            Assert.That(Game.S, Is.GreaterThanOrEqualTo(236f), "landed across the pit");
            Assert.That(slid, Is.True, "slid");
            Assert.That(slideJump, Is.GreaterThan(body.Def.sprintSpeed + 1f), "the slide jump carries the boost");
            Assert.That(wallEntry, Is.GreaterThan(slideJump - 0.8f), "the wall takes the speed");
            Assert.That(afterKick, Is.GreaterThan(wallEntry + 1f), "the wall kick adds speed");
            Assert.That(afterDouble, Is.GreaterThan(afterKick - 0.6f), "the double jump keeps it");
            Assert.That(top, Is.GreaterThan(12.5f));
        }

        [UnityTest]
        public IEnumerator TheAutopilotFinishesTheWholeCourse()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 2f;
            Game.JumpToChapter(LabCourse.ChapterStart);
            yield return Frames(2);
            int respawns = Game.Respawns;
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 75f;
            while (Game.Finishes == 0 && Time.realtimeSinceStartup < until) yield return null;
            Game.SetAutopilot(false);
            PrintTrace("Trace (state changes and respawns):");
            Assert.That(Game.Finishes, Is.GreaterThan(0), "the autopilot finished the course within 75 s (trace in the output)");
            TestContext.Out.WriteLine($"course time {Game.LastTime:0.0} s (game time), top speed {Game.TopSpeed:0.0} m/s, respawns {Game.Respawns - respawns}, caught {Game.Caught}");
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip on the real CharacterController");
            Assert.That(Game.TopSpeed, Is.GreaterThan(14f));
        }

        [UnityTest]
        public IEnumerator StandingStillInTheChaseGetsYouCaughtAndPutBack()
        {
            yield return StartLab();
            Game.JumpToChapter(LabCourse.ChapterChase);
            yield return Frames(2);
            Assert.That(Game.Chasing, Is.True);
            Assert.That(Game.Gap, Is.EqualTo(Game.Course.pursuer.targetGap).Within(1.5f), "the static starts its target gap behind");
            Game.player.InputEnabled = false;
            yield return WaitFor(() => Game.Caught > 0, "the static to catch a player who stops", 10f);
            yield return RespawnDone();
            Game.player.InputEnabled = true;
            int cp = Game.Course.CheckpointIndex(LabCourse.ChapterChase);
            Assert.That(Game.Checkpoint, Is.EqualTo(cp));
            Assert.That(Game.Gap, Is.GreaterThan(8f), "put back with room to run");
        }

        [UnityTest]
        public IEnumerator FallingBelowTheCourseRespawnsAtTheCheckpoint()
        {
            yield return StartLab();
            Game.JumpToChapter(LabCourse.ChapterWallruns);
            yield return Frames(2);
            int before = Game.Respawns;
            // A player who keeps holding forward through the fall (letting go stops you: Source friction).
            Game.player.Autopilot = sim => new MoveInput { move = new System.Numerics.Vector2(0f, 1f), yaw = 0f, sprintHeld = true }; // forward with sprint held (sprint is opt-in)
            Game.player.Teleport(new Vector3(0f, -10f, 145f), 0f, Vector3.zero);
            yield return WaitFor(() => Game.Respawns > before, "the respawn", 5f);
            yield return RespawnDone();
            Game.player.Autopilot = null;
            TestContext.Out.WriteLine($"respawned at {Game.LastRespawnSpeed:0.0} m/s, {Game.player.Speed:0.0} m/s after the fade");
            Assert.That(Game.LastRespawnSpeed, Is.EqualTo(Game.player.Def.sprintSpeed).Within(0.1f), "the respawn launches you at sprint");
            var cp = Game.Course.checkpoints[Game.Course.CheckpointIndex(LabCourse.ChapterWallruns)];
            var at = Game.player.transform.position;
            TestContext.Out.WriteLine("back at " + at + ", checkpoint " + cp.position);
            Assert.That(at.z, Is.InRange(cp.position.Z - 0.5f, cp.position.Z + 8f), "back at the checkpoint, running on from it");
            Assert.That(at.y, Is.EqualTo(cp.position.Y).Within(0.5f));
            Assert.That(Game.player.Speed, Is.GreaterThan(8.5f), "holding forward and sprint, you come back running");
        }

        [UnityTest]
        public IEnumerator EveryChapterJumpsCleanly()
        {
            yield return StartLab();
            foreach (var name in MovementLabPresenter.Chapters)
            {
                Assert.That(PresenterMode.Jump(MovementLabPresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                var cp = Game.Course.checkpoints[Game.Course.CheckpointIndex(name)];
                Assert.That(Game.Checkpoint, Is.EqualTo(Game.Course.CheckpointIndex(name)), name);
                Assert.That(Mathf.Abs(Game.player.transform.position.z - cp.position.Z), Is.LessThan(2f), name);
                Assert.That(Game.Chasing, Is.EqualTo(name == LabCourse.ChapterChase), name + ": the chase runs only in the chase");
                Assert.That(GameRoot.Ui.Top, Is.Null, name);
            }
        }

        [UnityTest]
        public IEnumerator ComfortSettingsReachTheCameraAndThePauseScreenHasThem()
        {
            yield return StartLab();
            var cam = Game.player.cam;
            GameRoot.Settings.fieldOfView = 80f;
            GameRoot.Settings.motionEffects = 0f;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(cam.fieldOfView, Is.EqualTo(80f).Within(0.5f), "the field of view follows the setting (no speed kick at 0 effects)");
            GameRoot.Instance.Pause();
            yield return Frames(2);
            Assert.That(GameRoot.Ui.Pause.Activate(MovementComfortScreen.PauseButton), Is.True, "Pause has Comfort and camera");
            yield return Frames(2);
            Assert.That(GameRoot.Ui.Top, Is.InstanceOf<MovementComfortScreen>());
            GameRoot.Ui.Back();
            yield return Frames(2);
            GameRoot.Instance.Resume();
            yield return Frames(2);
        }
    
        [UnityTest]
        public IEnumerator NadiasBodyCarriesTheMoves()
        {
            yield return StartLab();
            // The wallrun section: several catches.
            Game.JumpToChapter(LabCourse.ChapterWallruns);
            yield return Frames(2);
            var walls = new BodyTestUtil.Report();
            Game.SetAutopilot(true);
            yield return BodyTestUtil.Watch(Game.player, () => walls.catchesChecked >= 3, 20f, walls);
            Game.SetAutopilot(false);
            BodyTestUtil.AssertFollowed(walls, true, false);
            // The momentum chain: the slide.
            Game.JumpToChapter(LabCourse.ChapterChain);
            yield return Frames(2);
            var slides = new BodyTestUtil.Report();
            Game.SetAutopilot(true);
            yield return BodyTestUtil.Watch(Game.player, () => Game.S >= 236f && Game.player.Sim.Grounded, 12f, slides);
            Game.SetAutopilot(false);
            BodyTestUtil.AssertFollowed(slides, false, true);
        }
    }
}

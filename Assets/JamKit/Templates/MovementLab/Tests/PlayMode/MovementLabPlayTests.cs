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
            yield return WaitFor(() => Game.Finishes > 0, "the autopilot to finish the course", 75f);
            Game.SetAutopilot(false);
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
            Game.player.InputEnabled = true;
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the respawn fade", 3f);
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
            Game.player.Teleport(new Vector3(0f, -10f, 145f), 0f, Vector3.zero);
            yield return WaitFor(() => Game.Respawns > before, "the respawn", 5f);
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the fade", 3f);
            var cp = Game.Course.checkpoints[Game.Course.CheckpointIndex(LabCourse.ChapterWallruns)];
            Assert.That(Vector3.Distance(Game.player.transform.position, new Vector3(cp.position.X, cp.position.Y, cp.position.Z)), Is.LessThan(4f));
            Assert.That(Game.player.Speed, Is.GreaterThan(5f), "you come back running");
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
    }
}

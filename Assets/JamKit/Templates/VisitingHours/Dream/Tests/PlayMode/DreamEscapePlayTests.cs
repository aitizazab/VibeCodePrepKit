using JamKit.Tests.Play;
using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Visiting Hours' first escape in its real scene, on Unity's CharacterController: the autopilot runs
    /// the whole escape to the light without being caught (both collapses fire); standing still on the roofs gets
    /// you caught and put back at the checkpoint, running, with the static at its distance; waiting in the
    /// driveway wakes the static; a dream drop respawns you; every chapter jumps cleanly; the cast turns to
    /// watch you. Traces and speeds go to the test output.</summary>
    public class DreamEscapePlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null)
            {
                Game.SetAutopilot(false);
                Game.player.InputEnabled = true;
            }
            PresenterMode.Close();
            yield return null;
        }

        static DreamEscapeGame Game => DreamEscapeGame.Current;

        static IEnumerator RespawnDone() => WaitFor(() => !Game.Respawning && Game.WhiteAlpha == 0f, "the respawn to finish", 5f);

        static IEnumerator StartEscape()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DreamEscapePresenter.Scene), Is.True, "DreamEscape is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamEscapePresenter.Scene
                && Game != null && Game.Course != null, "the dream escape scene");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheAutopilotEscapesWithoutBeingCaught()
        {
            yield return StartEscape();
            DebugMenu.TimeScale = 2f;
            Game.JumpToChapter(DreamEscapeCourse.ChapterStart);
            yield return Frames(2);
            int respawns = Game.Respawns, caught = Game.Caught;
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 70f;
            float underDoor = float.MaxValue; // the slowest speed while passing under the garage door (z 205.2-206.8)
            while (Game.Finishes == 0 && Time.realtimeSinceStartup < until)
            {
                float z = Game.player.transform.position.z;
                if (z > 205.2f && z < 206.8f && !Game.Respawning) underDoor = Mathf.Min(underDoor, Game.player.Speed);
                yield return null;
            }
            Game.SetAutopilot(false);
            TestContext.Out.WriteLine($"slowest under the garage door: {underDoor:0.0} m/s");
            Assert.That(underDoor, Is.GreaterThan(7f), "the slide carries its speed under the garage door (no dead stop at its face)");
            TestContext.Out.WriteLine("Trace (state changes and respawns):");
            TestContext.Out.WriteLine(Game.TraceText);
            TestContext.Out.WriteLine($"escape {Game.LastTime:0.0} s (game time), top speed {Game.TopSpeed:0.0} m/s, respawns {Game.Respawns - respawns}, caught {Game.Caught - caught}, collapses {Game.CollapsesFired}");
            Assert.That(Game.Finishes, Is.GreaterThan(0), "the autopilot reached the light within 70 s (trace in the output)");
            Assert.That(Game.Caught - caught, Is.EqualTo(0), "the static never caught it");
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip on the real CharacterController");
            Assert.That(Game.CollapsesFired, Is.EqualTo(2), "the fences folded and the house fronts peeled");
            Assert.That(Game.TopSpeed, Is.GreaterThan(12f));
            Assert.That(Game.LastTime, Is.InRange(35f, 90f));
        }

        [UnityTest]
        public IEnumerator StandingStillOnTheRoofsGetsYouCaughtAndPutBackRunning()
        {
            yield return StartEscape();
            Game.JumpToChapter(DreamEscapeCourse.ChapterRooftops);
            yield return Frames(2);
            Assert.That(Game.Chasing, Is.True);
            Assert.That(Game.Gap, Is.EqualTo(Game.Course.pursuer.targetGap).Within(1.5f), "the static starts its target gap behind");
            Game.player.InputEnabled = false;
            float threatSeen = 0f, scouts = 0f;
            float end = Time.realtimeSinceStartup + 10f;
            while (Game.Caught == 0 && Time.realtimeSinceStartup < end)
            {
                threatSeen = Mathf.Max(threatSeen, Game.music.Threat);
                scouts = Mathf.Max(scouts, Game.staticView.ScoutsOut);
                yield return null;
            }
            Assert.That(Game.Caught, Is.GreaterThan(0), "the static catches a player who stops");
            Assert.That(threatSeen, Is.GreaterThan(0.5f), "the chase music swelled as it closed in");
            Assert.That(scouts, Is.GreaterThanOrEqualTo(3f), "wasps flew at the edges of the view");
            Assert.That(Game.Respawning, Is.True, "a quick white-out, then back at the checkpoint");
            yield return RespawnDone();
            int cp = Game.Course.CheckpointIndex(DreamEscapeCourse.ChapterRooftops);
            Assert.That(Game.Checkpoint, Is.EqualTo(cp));
            Assert.That(Game.Gap, Is.GreaterThan(8f), "put back with room to run");
            Assert.That(Game.LaunchSpeed, Is.GreaterThanOrEqualTo(Game.player.Def.sprintSpeed - 0.01f), "back with momentum");
            var at = Game.player.transform.position;
            var pos = Game.Course.checkpoints[cp].position;
            Assert.That(at.z, Is.InRange(pos.Z - 0.5f, pos.Z + 10f), "back at the checkpoint");
            Game.player.InputEnabled = true;
        }

        [UnityTest]
        public IEnumerator WaitingInTheDrivewayWakesTheStatic()
        {
            yield return StartEscape();
            Game.JumpToChapter(DreamEscapeCourse.ChapterStart);
            yield return Frames(2);
            Assert.That(Game.Chasing, Is.False, "the static sleeps until you run");
            Game.player.InputEnabled = false;
            yield return WaitFor(() => Game.Chasing, "the static to wake by itself", Game.Dream.wakeAfter + 2f);
            Assert.That(Game.staticView.Shown, Is.True);
            Game.player.InputEnabled = true;
        }

        [UnityTest]
        public IEnumerator ADreamDropPutsYouBackAtTheCheckpoint()
        {
            yield return StartEscape();
            Game.JumpToChapter(DreamEscapeCourse.ChapterRooftops);
            yield return Frames(2);
            int before = Game.Respawns, falls = Game.Falls;
            Game.player.Teleport(new Vector3(0f, -13f, 265f), 0f, Vector3.zero);
            yield return WaitFor(() => Game.Respawns > before, "the respawn", 5f);
            yield return RespawnDone();
            Assert.That(Game.Falls, Is.EqualTo(falls + 1));
            var cp = Game.Course.checkpoints[Game.Course.CheckpointIndex(DreamEscapeCourse.ChapterRooftops)];
            var at = Game.player.transform.position;
            Assert.That(at.y, Is.EqualTo(cp.position.Y).Within(0.5f));
            Assert.That(at.z, Is.InRange(cp.position.Z - 0.5f, cp.position.Z + 10f));
            Assert.That(Game.LaunchSpeed, Is.GreaterThan(5f), "you come back running");
        }

        [UnityTest]
        public IEnumerator EveryChapterJumpsCleanly()
        {
            yield return StartEscape();
            foreach (var name in DreamEscapePresenter.Chapters)
            {
                Assert.That(PresenterMode.Jump(DreamEscapePresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                int idx = Game.Course.CheckpointIndex(name);
                var cp = Game.Course.checkpoints[idx];
                Assert.That(Game.Checkpoint, Is.EqualTo(idx), name);
                Assert.That(Mathf.Abs(Game.player.transform.position.z - cp.position.Z), Is.LessThan(2f), name);
                Assert.That(Game.Chasing, Is.EqualTo(name != DreamEscapeCourse.ChapterStart), name + ": the static runs except at the very start");
                Assert.That(GameRoot.Ui.Top, Is.Null, name);
                Assert.That(Game.WhiteAlpha, Is.EqualTo(0f), name);
            }
        }

        [UnityTest]
        public IEnumerator TheCastTurnsToWatchYou()
        {
            yield return StartEscape();
            Game.JumpToChapter(DreamEscapeCourse.ChapterStart);
            Game.player.InputEnabled = false;
            DreamCutoutView walt = null;
            foreach (var c in Object.FindObjectsByType<DreamCutoutView>(FindObjectsSortMode.None))
                if (c.name.StartsWith("Walt")) walt = c;
            Assert.That(walt, Is.Not.Null);
            yield return new WaitForSeconds(1.5f);
            Vector3 d = Game.player.cam.transform.position - walt.transform.position;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float off = Mathf.Abs(Mathf.DeltaAngle(walt.Yaw, want));
            TestContext.Out.WriteLine($"Walt faces {walt.Yaw:0} degrees, the player is at {want:0} ({off:0} off)");
            Assert.That(off, Is.LessThan(25f), "Walt turned to watch Nadia");
            Game.player.InputEnabled = true;
        }
    
        [UnityTest]
        public IEnumerator NadiasBodyCarriesTheMovesInTheDream()
        {
            yield return StartEscape();
            DebugMenu.TimeScale = 2f;
            Game.JumpToChapter(DreamEscapeCourse.ChapterStart);
            yield return Frames(2);
            var report = new BodyTestUtil.Report();
            Game.SetAutopilot(true);
            yield return BodyTestUtil.Watch(Game.player, () => Game.Finishes > 0, 70f, report);
            Game.SetAutopilot(false);
            DebugMenu.TimeScale = 1f;
            BodyTestUtil.AssertFollowed(report, true, true);
        }
    }
}

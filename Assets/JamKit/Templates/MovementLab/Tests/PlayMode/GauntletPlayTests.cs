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

namespace JamKit.MovementLab.Tests
{
    /// <summary>The Gauntlet in the Movement Lab on Unity's CharacterController: the autopilot runs it from the start
    /// pad through the finish, touching every marker and recording a ghost (its run doesn't count for your best);
    /// the restart puts the clock and markers back; a skipped section's markers add their penalty; the chapters land
    /// where they should. Times go to the test output.</summary>
    public class GauntletPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.SetAutopilot(false);
            PresenterMode.Close();
            yield return null;
        }

        static MovementLabGame Game => MovementLabGame.Current;
        static GauntletRun Run => GauntletRun.Current;

        static IEnumerator StartLab()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True, "MovementLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && Game != null && Game.Gauntlet != null && Run != null && Run.Clock != null, "the Movement Lab and its Gauntlet");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheLabOpensOnTheStartPadReady()
        {
            yield return StartLab();
            var g = Game.Gauntlet;
            Assert.That(Game.Checkpoint, Is.EqualTo(g.startCheckpoint));
            Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready));
            Assert.That(Game.player.transform.position.z, Is.LessThan(g.gateS), "before the gate");
        }

        [UnityTest]
        public IEnumerator TheAutopilotRunsTheGauntletTouchingEveryMarker()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 2f;
            Run.Restart();
            yield return Frames(2);
            float bestBefore = Run.Clock.record.best;
            int respawns = Game.Respawns;
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 60f;
            while (Run.Clock.State != GauntletState.Finished && Time.realtimeSinceStartup < until) yield return null;
            Game.SetAutopilot(false);
            var clock = Run.Clock;
            TestContext.Out.WriteLine(Game.TraceText);
            TestContext.Out.WriteLine($"Gauntlet {clock.Time:0.00} s (autopilot's time in the sim {GauntletCourse.AutopilotTime:0.0}), markers {clock.MarkersHit}/{clock.MarkerCount}, respawns {Game.Respawns - respawns}, ghost {Run.LiveGhost.Count} samples over {Run.LiveGhost.Duration:0.00} s");
            for (int i = 0; i < Game.Gauntlet.sections.Count; i++) TestContext.Out.WriteLine($"  {Game.Gauntlet.sections[i].name}: {clock.Split(i):0.00}");
            Assert.That(clock.State, Is.EqualTo(GauntletState.Finished), "the autopilot crossed the finish line within 60 s (trace in the output)");
            Assert.That(clock.Missed, Is.EqualTo(0), "every marker touched");
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip on the real CharacterController");
            Assert.That(clock.Time, Is.InRange(GauntletCourse.AutopilotTime - 5f, GauntletCourse.AutopilotTime + 12f), "near the autopilot's time in the sim");
            Assert.That(Run.LiveGhost.Count, Is.GreaterThan(100), "a ghost was recorded");
            Assert.That(Run.LiveGhost.Duration, Is.EqualTo(clock.Time).Within(1.5f), "the ghost covers the run");
            Assert.That(Run.Autopiloted, Is.True);
            Assert.That(Run.Clock.record.best, Is.EqualTo(bestBefore), "the autopilot's run doesn't count for your best");
            yield return Frames(3);
            Assert.That(Run.EndScreenShown, Is.True, "the end screen");
        }

        [UnityTest]
        public IEnumerator RestartPutsTheClockAndMarkersBack()
        {
            yield return StartLab();
            Run.Restart();
            yield return Frames(2);
            Game.SetAutopilot(true);
            yield return WaitFor(() => Run.Clock.State == GauntletState.Running && Run.Clock.MarkersHit >= 1 && Run.Clock.Time > 3f, "the run to start and touch a marker", 20f);
            Assert.That(Run.markerViews[0].Current, Is.EqualTo(GauntletMarkerView.Look.Touched));
            Game.SetAutopilot(false);
            Run.Restart();
            yield return Frames(3);
            var g = Game.Gauntlet;
            Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready));
            Assert.That(Run.Clock.Time, Is.EqualTo(0f));
            Assert.That(Run.Clock.MarkersHit, Is.EqualTo(0));
            foreach (var v in Run.markerViews) Assert.That(v.Current, Is.EqualTo(GauntletMarkerView.Look.Waiting));
            Assert.That(Game.Checkpoint, Is.EqualTo(g.startCheckpoint));
            var start = g.course.checkpoints[g.startCheckpoint].position;
            Assert.That(Game.player.transform.position.z, Is.EqualTo(start.Z).Within(1f), "back on the start pad");
            Assert.That(Game.player.Speed, Is.LessThan(1f), "standing");
            Assert.That(Run.GhostVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator SkippingSectionsAddsTheMissedMarkersPenalty()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 2f;
            Run.Restart();
            yield return Frames(2);
            Game.SetAutopilot(true);
            yield return WaitFor(() => Run.Clock.State == GauntletState.Running && Run.Clock.Time > 0.5f, "the run to start", 15f);
            // Jump ahead to the last section with the clock running: every marker in between is missed.
            Game.JumpToChapter(GauntletCourse.ChapterFinish);
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 30f;
            while (Run.Clock.State != GauntletState.Finished && Time.realtimeSinceStartup < until) yield return null;
            Game.SetAutopilot(false);
            var clock = Run.Clock;
            TestContext.Out.WriteLine($"time {clock.Time:0.00}, missed {clock.Missed}, total {clock.Total:0.00}");
            Assert.That(clock.State, Is.EqualTo(GauntletState.Finished));
            Assert.That(clock.Missed, Is.GreaterThanOrEqualTo(Game.Gauntlet.markers.Count - 2), "the skipped sections' markers");
            Assert.That(clock.Total, Is.EqualTo(clock.Time + clock.Missed * GauntletCourse.MissPenalty).Within(0.01f), "2 s a missed marker");
        }

        [UnityTest]
        public IEnumerator RestartAndGhostFollowTheirRebinds()
        {
            yield return StartLab();
            var input = GameRoot.Input;
            var restartRow = input.Bindings.FindRow("restart");
            Assert.That(restartRow.Shown && input.Bindings.FindRow("ghost").Shown, Is.True, "the lab shows its Controls rows");
            var kb = new TestInput("GauntletTestKeyboard");
            try
            {
                yield return null;
                // Restart moves from R to T (Settings > Controls does the same through SetPath).
                input.Bindings.SetPath(input.Restart, restartRow.Primary(ControlColumn.KeyboardMouse), "<Keyboard>/t");
                Run.Restart();
                yield return Frames(2);
                Game.SetAutopilot(true);
                yield return WaitFor(() => Run.Clock.State == GauntletState.Running && Run.Clock.Time > 1f, "the run to start", 20f);
                Game.SetAutopilot(false);

                yield return kb.Press(Key.R);
                yield return kb.Press();
                Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Running), "R doesn't restart any more");
                yield return kb.Press(Key.T);
                yield return kb.Press();
                Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready), "T restarts the run");
                Assert.That(Game.Checkpoint, Is.EqualTo(Game.Gauntlet.startCheckpoint), "back on the start pad");

                // The ghost toggle, on its default G.
                var ghostWas = Run.GhostChoice;
                yield return kb.Press(Key.G);
                yield return kb.Press();
                Assert.That(Run.GhostChoice, Is.Not.EqualTo(ghostWas), "G changes the ghost (yours, the developer's, off)");
            }
            finally
            {
                kb.Dispose();
                input.Bindings.ResetAll();
            }
        }

        [UnityTest]
        public IEnumerator DpadLeftTogglesTheGhostWithoutMovingYou()
        {
            yield return StartLab();
            var input = GameRoot.Input;
            Assert.That(input.Bindings.MoveDpad, Is.False, "the lab doesn't put the d-pad on Move");
            var io = new TestInput("GauntletDpadKeyboard");
            io.AddGamepad("GauntletDpadPad");
            try
            {
                Run.Restart();
                yield return Frames(5);
                Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready));
                var before = Game.player.transform.position;
                var ghostWas = Run.GhostChoice;

                // Hold d-pad left for half a second: one ghost toggle, no movement.
                io.SetPad(GamepadButton.DpadLeft);
                float until = Time.realtimeSinceStartup + 0.5f;
                bool moved = false;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    if (input.Move.ReadValue<Vector2>().sqrMagnitude > 0.0001f) moved = true;
                }
                Assert.That(moved, Is.False, "Move read nothing from the d-pad");
                Assert.That(Run.GhostChoice, Is.Not.EqualTo(ghostWas), "d-pad left toggled the ghost (once: it's a press)");
                var after = Game.player.transform.position;
                Assert.That(new Vector2(after.x - before.x, after.z - before.z).magnitude, Is.LessThan(0.05f), "the player stayed put");
                io.SetPad();
                yield return Frames(2);
                Assert.That(Run.GhostChoice, Is.Not.EqualTo(ghostWas), "releasing doesn't toggle it back");
            }
            finally
            {
                io.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator TheBoardShowsTheDevelopersRealBestAndTheirGhostRacesYou()
        {
            yield return StartLab();
            Run.Restart();
            yield return Frames(5);

            // The board: the creator's real best from the shipped records (penalty included), Mags at 1.5x rounded.
            Assert.That(Run.DeveloperTime, Is.EqualTo(31.42f).Within(0.01f), "Data/DeveloperRecords.json");
            Assert.That(Run.DeveloperMissed, Is.EqualTo(1));
            Assert.That(Run.MagsTime, Is.EqualTo(47.5f));
            yield return WaitFor(() => Run.BoardShown != null && Run.BoardShown.Contains("31.42"), "the board's words", 2f);
            TestContext.Out.WriteLine(Run.BoardShown);
            StringAssert.Contains("47.50", Run.BoardShown);
            StringAssert.Contains("1 plate missed", Run.BoardShown);
            StringAssert.DoesNotContain(GauntletCourse.AutopilotTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), Run.BoardShown, "the autopilot's time isn't a target");

            // The developer's ghost loads, and the cycle offers it.
            Assert.That(Run.DeveloperGhost.Count, Is.EqualTo(589), "the creator's run at 20 Hz");
            Assert.That(Run.Ghost.Has(GhostChoice.Developer), Is.True);
            Run.Ghost.Set(GhostChoice.Off);
            Run.Ghost.Cycle();
            Assert.That(Run.GhostChoice, Is.Not.EqualTo(GhostChoice.Off), "off → your best (or the developer's if you have none)");
            Run.Ghost.Set(GhostChoice.Yours);
            Run.Ghost.Cycle();
            Assert.That(Run.GhostChoice, Is.EqualTo(GhostChoice.Developer), "your best → the developer's");
            Run.Ghost.Cycle();
            Assert.That(Run.GhostChoice, Is.EqualTo(GhostChoice.Off), "the developer's → off");
            Run.Ghost.Set(GhostChoice.Developer);
            StringAssert.Contains("developer", Run.Ghost.Label);

            // It plays: once the clock runs, the gold figure stands where the creator was at that time.
            DebugMenu.TimeScale = 2f;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Run.Clock.State == GauntletState.Running && Run.Clock.Time > 3f, "three seconds into the run", 20f);
            Assert.That(Run.GhostVisible, Is.True, "the developer's ghost shows while the clock runs");
            yield return null;
            float t = Run.Clock.Time;
            Assert.That(Run.DeveloperGhost.Sample(t, out var p, out _), Is.True);
            var at = Run.ghost.position;
            float off = Vector3.Distance(at, new Vector3(p.X, p.Y, p.Z));
            TestContext.Out.WriteLine($"at {t:0.00} s the ghost is at z {at.z - GauntletCourse.Origin:0.0}, the record says {p.Z - GauntletCourse.Origin:0.0} (off by {off:0.00} m); you're at {Game.player.transform.position.z - GauntletCourse.Origin:0.0}");
            Assert.That(off, Is.LessThan(3f), "the figure follows the developer's record (a frame or so behind at most)");
            Game.SetAutopilot(false);
            Run.Restart();
            yield return Frames(3);
            Assert.That(Run.GhostVisible, Is.False, "hidden on the start pad");
        }

        [UnityTest]
        public IEnumerator TheGauntletChaptersLandOnTheirSections()
        {
            yield return StartLab();
            var g = Game.Gauntlet;
            foreach (var name in new[] { GauntletCourse.ChapterStart, GauntletCourse.ChapterTeaching, GauntletCourse.ChapterCombo, GauntletCourse.ChapterFinish })
            {
                Assert.That(PresenterMode.Jump(MovementLabPresenter.Scene, name), Is.EqualTo(JumpResult.Jumped), name);
                yield return Frames(3);
                int cp = g.course.CheckpointIndex(name);
                Assert.That(Game.Checkpoint, Is.EqualTo(cp), name);
                Assert.That(Mathf.Abs(Game.player.transform.position.z - g.course.checkpoints[cp].position.Z), Is.LessThan(2f), name);
                Assert.That(Game.Chasing, Is.False, name);
            }
        }
    }
}

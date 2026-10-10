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
    /// <summary>The dream tool chapter on Unity's CharacterController: the autopilot, firing the tool at its cues, clears
    /// all three sections (glass, builds, colour flips) and gets the end screen without counting for your best; with the
    /// tool held, a glass tile stops it in the lane until it's shot; a switch makes the other colour solid (colliders and
    /// a raycast agree); the restart puts the clock and the tiles back. Times go to the test output.</summary>
    public class DreamToolPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.SetAutopilot(false);
            if (Run != null && Run.tool != null) Run.tool.autoFire = true;
            PresenterMode.Close();
            yield return null;
        }

        static MovementLabGame Game => MovementLabGame.Current;
        static DreamToolRun Run => DreamToolRun.Current;

        static IEnumerator StartChapter()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True, "MovementLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && Game != null && Game.DreamCourse != null && Run != null && Run.State != null, "the Movement Lab and its dream tool chapter");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            Assert.That(PresenterMode.Jump(MovementLabPresenter.Scene, DreamToolCourse.Chapter), Is.EqualTo(JumpResult.Jumped));
            yield return Frames(3);
            Assert.That(Game.Checkpoint, Is.EqualTo(Game.DreamCourse.startCheckpoint), "on the chapter's start pad");
            Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready));
        }

        [UnityTest]
        public IEnumerator TheAutopilotClearsAllThreeSections()
        {
            yield return StartChapter();
            var d = Game.DreamCourse;
            DebugMenu.TimeScale = 2f;
            float bestBefore = Run.Clock.record.best;
            int bestGhostBefore = Run.BestGhost.Count;
            int respawns = Game.Respawns;
            Assert.That(Run.DeveloperTime, Is.EqualTo(28.17f).Within(0.01f), "the board's developer time is the creator's real best");
            Assert.That(Run.MagsTime, Is.EqualTo(42.5f));
            yield return WaitFor(() => Run.BoardShown != null && Run.BoardShown.Contains("28.17"), "the board's words", 2f);
            StringAssert.Contains("42.50", Run.BoardShown);
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 60f;
            while (Run.Clock.State != GauntletState.Finished && Time.realtimeSinceStartup < until) yield return null;
            Game.SetAutopilot(false);
            var clock = Run.Clock;
            TestContext.Out.WriteLine(Game.TraceText);
            TestContext.Out.WriteLine($"Dream tool {clock.Time:0.00} s (autopilot's time in the sim {DreamToolCourse.AutopilotTime:0.0}), targets {Run.State.RequiredDone}/{d.RequiredCount}, shots {Run.State.Shots}, respawns {Game.Respawns - respawns}");
            for (int i = 0; i < d.run.sections.Count; i++) TestContext.Out.WriteLine($"  {d.run.sections[i].name}: {clock.Split(i):0.00}");
            Assert.That(clock.State, Is.EqualTo(GauntletState.Finished), "the autopilot crossed the finish within 60 s (trace in the output)");
            Assert.That(clock.SectionsDone, Is.EqualTo(3), "glass, builds and the colour puzzle");
            Assert.That(Run.State.RequiredDone, Is.EqualTo(d.RequiredCount), "every tile popped, socket built and switch flipped");
            Assert.That(Game.Respawns - respawns, Is.LessThanOrEqualTo(1), "at most one slip on the real CharacterController");
            Assert.That(clock.Time, Is.InRange(DreamToolCourse.AutopilotTime - 5f, DreamToolCourse.AutopilotTime + 12f), "near the autopilot's time in the sim");
            Assert.That(Run.Autopiloted, Is.True);
            Assert.That(clock.record.best, Is.EqualTo(bestBefore), "the autopilot's run doesn't count for your best");
            TestContext.Out.WriteLine($"ghost {Run.LiveGhost.Count} samples over {Run.LiveGhost.Duration:0.00} s");
            Assert.That(Run.LiveGhost.Count, Is.GreaterThan(100), "the run recorded a ghost (it saved none before)");
            Assert.That(Run.LiveGhost.Duration, Is.EqualTo(clock.Time).Within(1.5f), "the ghost covers the run");
            Assert.That(Run.BestGhost.Count, Is.EqualTo(bestGhostBefore), "nor for your best ghost");
            yield return Frames(3);
            Assert.That(Run.EndScreenShown, Is.True, "the end screen");
        }

        [UnityTest]
        public IEnumerator AMissedTileBlocksTheLaneUntilItsShot()
        {
            yield return StartChapter();
            var d = Game.DreamCourse;
            int pane = d.targets.FindIndex(t => t.kind == DreamTargetKind.Tile);
            int piece = d.pieces.FindIndex(p => p.target == pane);
            DebugMenu.TimeScale = 2f;
            Run.tool.autoFire = false;
            int respawns = Game.Respawns;
            Game.SetAutopilot(true);
            yield return WaitFor(() => Game.player.transform.position.z > d.targets[pane].S - 6f, "the run up to the first pane", 20f);
            float t0 = Time.time;
            while (Time.time - t0 < 3f) yield return null;
            float z = Game.player.transform.position.z;
            TestContext.Out.WriteLine($"stopped at z {z - DreamToolCourse.Origin:0.00}, pane at {d.targets[pane].S - DreamToolCourse.Origin:0.00}, speed {Game.player.Speed:0.0}");
            Assert.That(z, Is.LessThan(d.targets[pane].S), "the glass stops you");
            Assert.That(z, Is.GreaterThan(d.targets[pane].S - 2f), "right against it");
            Assert.That(Game.player.Speed, Is.LessThan(2f), "running into it, not through it");
            Assert.That(Game.Respawns, Is.EqualTo(respawns), "blocked, not fallen");
            Assert.That(Run.pieceViews[piece].solidCollider.enabled, Is.True);
            Assert.That(Run.State.Done(pane), Is.False);

            // Shoot it: the lane opens and the run goes on.
            Run.tool.autoFire = true;
            yield return WaitFor(() => Game.player.transform.position.z > d.targets[pane].S + 3f, "the run through the open doorway", 10f);
            Assert.That(Run.State.Done(pane), Is.True);
            Assert.That(Run.pieceViews[piece].solidCollider.enabled, Is.False);
            Game.SetAutopilot(false);

            // The restart: the clock and the glass come back.
            Run.Restart();
            yield return Frames(3);
            Assert.That(Run.Clock.State, Is.EqualTo(GauntletState.Ready));
            Assert.That(Run.State.Done(pane), Is.False);
            Assert.That(Run.pieceViews[piece].solidCollider.enabled, Is.True, "the pane is back");
            Assert.That(Game.Checkpoint, Is.EqualTo(d.startCheckpoint));
        }

        [UnityTest]
        public IEnumerator AFlipMakesTheOtherColourSolid()
        {
            yield return StartChapter();
            var d = Game.DreamCourse;
            int puzzle = d.run.sections[2].checkpoint;
            Game.PlaceAtCheckpoint(puzzle, false);
            yield return Frames(2);
            Assert.That(Run.State.Colour, Is.EqualTo(DreamToolCourse.Pink), "the puzzle starts pink");
            int pinkPiece = d.pieces.FindIndex(p => p.role == DreamPieceRole.Pink);
            int bluePiece = d.pieces.FindIndex(p => p.role == DreamPieceRole.Blue);
            var blue = d.pieces[bluePiece];
            Assert.That(Run.pieceViews[pinkPiece].solidCollider.enabled, Is.True);
            Assert.That(Run.pieceViews[bluePiece].solidCollider.enabled, Is.False);
            yield return new WaitForFixedUpdate();
            var above = new Vector3(blue.Center.X, blue.max.Y + 3f, blue.Center.Z);
            bool hitBefore = Physics.Raycast(above, Vector3.down, out var h0, 3.5f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            Assert.That(hitBefore, Is.False, "blue is a ghost while pink is solid");

            int sw = d.targets.FindIndex(t => t.kind == DreamTargetKind.Switch);
            var shot = Run.tool.FireAt(sw);
            Assert.That(shot.fired, Is.True);
            Assert.That(shot.colour, Is.EqualTo(DreamToolCourse.Blue));
            yield return new WaitForFixedUpdate();
            Assert.That(Run.State.Colour, Is.EqualTo(DreamToolCourse.Blue));
            Assert.That(Run.pieceViews[pinkPiece].solidCollider.enabled, Is.False, "pink goes");
            Assert.That(Run.pieceViews[bluePiece].solidCollider.enabled, Is.True, "blue comes");
            Assert.That(Physics.Raycast(above, Vector3.down, out var h1, 3.5f, ~(1 << 2), QueryTriggerInteraction.Ignore), Is.True, "you can stand on blue now");
            Assert.That(h1.point.y, Is.EqualTo(blue.max.Y).Within(0.05f));
            Assert.That(Run.pieceViews[bluePiece].renderers[0].sharedMaterial, Is.SameAs(Run.pieceViews[bluePiece].solidMaterial), "and it looks solid");

            // A fall puts back the checkpoint's colour.
            Game.PlaceAtCheckpoint(puzzle, false);
            yield return Frames(2);
            Assert.That(Run.State.Colour, Is.EqualTo(DreamToolCourse.Pink));
            Assert.That(Run.pieceViews[bluePiece].solidCollider.enabled, Is.False);
        }
    }
}

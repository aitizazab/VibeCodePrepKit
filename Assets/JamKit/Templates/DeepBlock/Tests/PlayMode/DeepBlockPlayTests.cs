using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;
using Block = JamKit.DeepBlock.DeepBlock;

namespace JamKit.DeepBlockLab.Tests
{
    /// <summary>The Deep Block lab on Unity's CharacterController and physics: the course autopilot runs each of the four
    /// routes to the hill gate; the night selector opens and shuts the switch props; in hide and seek Theo counts, then
    /// finds a player standing in the open garden (a "found you!" and a recount, never a fail), and the memories count.
    /// Route times go to the test output.</summary>
    public class DeepBlockPlayTests
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

        static DeepBlockGame Game => DeepBlockGame.Current;

        static IEnumerator StartLab()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DeepBlockPresenter.Scene), Is.True, "DeepBlock is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DeepBlockPresenter.Scene
                && Game != null && Game.Block != null, "the Deep Block lab");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheAutopilotRunsEachRouteToTheHillGate()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 2f;
            var failed = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Game.Block.routes.Count; i++)
            {
                var r = Game.Block.routes[i];
                Game.StartRoute(i);
                yield return Frames(2);
                Game.SetAutopilot(true);
                int done = Game.RoutesDone;
                float until = Time.realtimeSinceStartup + 45f;
                while (Game.RoutesDone == done && Time.realtimeSinceStartup < until) yield return null;
                var p = Game.player.transform.position;
                bool ok = Game.RoutesDone > done;
                TestContext.Out.WriteLine($"{r.name}: {(ok ? Game.RouteTime.ToString("0.0") + " s" : "did not reach the gate")}, ended at map x {p.z:0.0}, y {p.x:0.0}, h {p.y:0.0} ({Game.player.Mode})");
                if (!ok) failed.Add(r.name);
                Game.SetAutopilot(false);
            }
            Assert.That(failed, Is.Empty, "routes the autopilot didn't finish on the CharacterController");
        }

        [UnityTest]
        public IEnumerator TheNightSelectorOpensAndShutsTheSwitches()
        {
            yield return StartLab();
            BlockSwitchProp laneGate = null;
            foreach (var p in Object.FindObjectsByType<BlockSwitchProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.name.StartsWith("Lane top gate")) laneGate = p;
            Assert.That(laneGate, Is.Not.Null);
            Game.SetNight(1);
            yield return null;
            Assert.That(laneGate.gameObject.activeSelf, Is.False, "night 1: the lane's top gate is open");
            Game.SetNight(3);
            yield return null;
            Assert.That(laneGate.gameObject.activeSelf, Is.True, "night 3: shut");
            yield return WaitFor(() => Game.PanelText.Contains("Night 3"), "the panel saying night 3", 2f);
            StringAssert.Contains("S6 SHUT", Game.PanelText);
            Game.SetNight(1);
        }

        [UnityTest]
        public IEnumerator TheoFindsAPlayerStandingInTheOpenGardenAndCountsAgain()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 3f;
            Game.StartHideAndSeek(0.5f);
            yield return Frames(2);
            Assert.That(Game.theo.gameObject.activeSelf, Is.True);
            Assert.That(Game.memories.Total, Is.EqualTo(6));
            var at = Block.W(12f, 25f);
            Game.player.Teleport(new Vector3(at.X, at.Y, at.Z), 0f, Vector3.zero);
            yield return WaitFor(() => Game.TheoFinds > 0, "Theo finding the player", 60f);
            Assert.That(Game.CurrentMode, Is.EqualTo(DeepBlockGame.Mode.HideAndSeek), "found is a laugh, not a fail");
            yield return WaitFor(() => Game.theo.Counting, "Theo counting again", 10f);
        }
    }
}

using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.StealthLab.Tests
{
    /// <summary>The Stealth Lab on Unity's physics: the seeker's walking map is one piece; a seeker finds a player standing
    /// still in the open; hiding in the closet only delays it (its sweep opens the closet); the HUD reads the meter and the
    /// state; the five collectibles count down and announce the last; the restart puts everything back.</summary>
    public class StealthLabPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            PresenterMode.Close();
            yield return null;
        }

        static StealthLabGame Game => StealthLabGame.Current;

        static IEnumerator StartLab()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(StealthLabPresenter.Scene), Is.True, "StealthLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == StealthLabPresenter.Scene
                && Game != null && Game.seeker != null && Game.seeker.Brain != null && StealthPlayer.Current != null, "the Stealth Lab");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        static IEnumerator StandStill(Vector3 feet)
        {
            Game.player.Teleport(feet, 0f, Vector3.zero);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheSeekerFindsAPlayerStandingStillInTheOpen()
        {
            yield return StartLab();
            Assert.That(Game.seeker.Graph.Count, Is.EqualTo(StealthYard.Build().nodes.Count));
            Assert.That(Game.seeker.Graph.Connected(), Is.True, "its walking map, joined by physics, is one piece");
            Assert.That(HidingSpotView.Spots.Count, Is.EqualTo(StealthYard.Build().spots.Count));
            DebugMenu.TimeScale = 2f;
            Game.Begin(StealthLabGame.Round.HideAndSeek, 0f);
            yield return StandStill(new Vector3(-14f, 0f, -12f));
            Assert.That(StealthPlayer.Current.Hidden, Is.False, "out in the open");
            float t0 = Time.time;
            yield return WaitFor(() => Game.seeker.State == AlertState.Found, "the seeker finding the player", 60f);
            TestContext.Out.WriteLine($"found after {Time.time - t0:0.0} s of game time");
            Assert.That(Game.RoundOver, Is.True);
            Assert.That(Game.Founds, Is.EqualTo(1));
            Assert.That(Game.seeker.Frozen, Is.True, "the round is over");
            // The HUD's words refresh ten times a second (frames in batch mode take milliseconds).
            yield return WaitFor(() => Game.Hud.Word == "Found !", "the HUD saying Found !", 2f);
            Assert.That(Game.Hud.FillShown, Is.EqualTo(1f).Within(0.01f));

            // The restart: counting again, the meter empty.
            Game.Begin(StealthLabGame.Round.HideAndSeek);
            yield return Frames(3);
            Assert.That(Game.seeker.Counting, Is.True);
            Assert.That(Game.seeker.Meter, Is.EqualTo(0f));
            Assert.That(Game.RoundOver, Is.False);
        }

        [UnityTest]
        public IEnumerator HidingInTheClosetHidesYouUntilItsSweepOpensIt()
        {
            yield return StartLab();
            DebugMenu.TimeScale = 4f;
            Game.Begin(StealthLabGame.Round.HideAndSeek, 0f);
            yield return StandStill(new Vector3(-9.2f, 0f, 10.3f));
            Assert.That(StealthPlayer.Current.HiddenKind, Is.EqualTo(HideKind.Closet));
            yield return WaitFor(() => Game.Hud.Word.StartsWith("Hidden"), "the HUD saying Hidden (while it searches)", 2f);
            bool openedIt = false;
            Game.seeker.Found += s => openedIt = s.Brain.Events.foundInSpot >= 0;
            yield return WaitFor(() => Game.seeker.State == AlertState.Found, "the seeker's sweep reaching the closet", 60f);
            Assert.That(openedIt, Is.True, "found by checking the closet, not by sight");
        }

        [UnityTest]
        public IEnumerator TheFiveCollectiblesCountDownAndAnnounceTheLast()
        {
            yield return StartLab();
            Game.Begin(StealthLabGame.Round.Patrol);
            Game.seeker.Frozen = true;
            var counter = Game.collectibles;
            Assert.That(counter.Total, Is.EqualTo(5));
            Assert.That(counter.AnyLeft, Is.True);
            int all = 0;
            counter.AllFound += () => all++;
            var yard = StealthYard.Build();
            int left = 5;
            foreach (var c in yard.collectibles)
            {
                var feet = new Vector3(c.position.X, Mathf.Max(0f, c.position.Y - 0.5f), c.position.Z);
                Game.player.Teleport(feet, 0f, Vector3.zero);
                yield return Frames(4);
                left--;
                Assert.That(counter.Remaining, Is.EqualTo(left), c.id);
            }
            Assert.That(counter.AnyLeft, Is.False);
            Assert.That(all, Is.EqualTo(1), "the last one announced once");
            Assert.That(Game.RoundOver, Is.True, "all five without being found");
            Game.Begin(StealthLabGame.Round.Patrol);
            yield return Frames(2);
            Assert.That(counter.Remaining, Is.EqualTo(5), "the restart puts them back");
        }
    }
}

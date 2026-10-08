using System.Collections.Generic;
using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class GameFlowTests
    {
        static GameFlow InRun()
        {
            var flow = new GameFlow();
            flow.ShowTitle();
            flow.StartRun();
            return flow;
        }

        [Test]
        public void HappyPathBootToCredits()
        {
            var flow = new GameFlow();
            var log = new List<string>();
            flow.Changed += (f, t) => log.Add(f + ">" + t);
            Assert.That(flow.ShowTitle(), Is.True);
            Assert.That(flow.StartRun(), Is.True);
            Assert.That(flow.Win(), Is.True);
            Assert.That(flow.ShowCredits(), Is.True);
            Assert.That(flow.QuitToTitle(), Is.True);
            Assert.That(log, Is.EqualTo(new[]
                { "Boot>Title", "Title>Playing", "Playing>Victory", "Victory>Credits", "Credits>Title" }));
        }

        [Test]
        public void WhitelistRefusesNonsense()
        {
            var flow = new GameFlow();
            Assert.That(flow.StartRun(), Is.False, "boot must go through title");
            Assert.That(flow.Pause(), Is.False);
            flow.ShowTitle();
            Assert.That(flow.Win(), Is.False);
            Assert.That(flow.Pause(), Is.False, "no pausing the title screen");
            flow.StartRun();
            flow.Lose();
            Assert.That(flow.Pause(), Is.False, "no pausing game over");
            Assert.That(flow.Win(), Is.False, "can't win after losing");
            Assert.That(flow.TryGo(GameState.Boot), Is.False);
            Assert.That(flow.Current, Is.EqualTo(GameState.GameOver));
            Assert.That(flow.QuitToTitle(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameState.Title));
        }

        [Test]
        public void PauseResumeReturnsWhereItCameFrom()
        {
            var flow = InRun();
            Assert.That(flow.IsTimeFrozen, Is.False);
            Assert.That(flow.Pause(), Is.True);
            Assert.That(flow.IsTimeFrozen, Is.True);
            Assert.That(flow.Resume(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameState.Playing));

            Assert.That(flow.OpenDialogue(), Is.True);
            Assert.That(flow.IsTimeFrozen, Is.True);
            Assert.That(flow.TogglePause(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameState.Paused));
            Assert.That(flow.TogglePause(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameState.Dialogue));
            Assert.That(flow.CloseDialogue(), Is.True);
            Assert.That(flow.Resume(), Is.False, "nothing to resume");
        }

        [Test]
        public void RunStartedFiresOnStartRetryAndRestartButNotResume()
        {
            var flow = new GameFlow();
            int runs = 0;
            flow.RunStarted += () => runs++;
            flow.ShowTitle();
            flow.StartRun();
            Assert.That(runs, Is.EqualTo(1));
            flow.Pause();
            flow.Resume();
            Assert.That(runs, Is.EqualTo(1));
            Assert.That(flow.Restart(), Is.True, "restart from Playing stays in Playing");
            Assert.That(runs, Is.EqualTo(2));
            flow.Pause();
            Assert.That(flow.Restart(), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameState.Playing));
            Assert.That(runs, Is.EqualTo(3));
            flow.Lose();
            Assert.That(flow.StartRun(), Is.True, "retry from game over");
            Assert.That(runs, Is.EqualTo(4));
            flow.OpenDialogue();
            Assert.That(flow.Restart(), Is.False, "pause first");
            Assert.That(runs, Is.EqualTo(4));
        }

        [Test]
        public void DialogueCanEndTheGame()
        {
            var flow = InRun();
            flow.OpenDialogue();
            Assert.That(flow.Win(), Is.True);
            Assert.That(flow.InRun, Is.False);
        }
    }
}

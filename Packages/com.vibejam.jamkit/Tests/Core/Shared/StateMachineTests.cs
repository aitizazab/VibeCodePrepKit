using System.Collections.Generic;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class StateMachineTests
    {
        enum S { Menu, Playing, Paused, GameOver }

        [Test]
        public void EnterExitAndChangedFireInOrder()
        {
            var log = new List<string>();
            var sm = new StateMachine<S>(S.Menu)
                .OnExit(S.Menu, () => log.Add("exit menu"))
                .OnEnter(S.Playing, () => log.Add("enter playing"));
            sm.Changed += (f, t) => log.Add($"{f}->{t}");
            sm.Go(S.Playing);
            Assert.That(log, Is.EqualTo(new[] { "exit menu", "enter playing", "Menu->Playing" }));
            Assert.That(sm.Previous, Is.EqualTo(S.Menu));
        }

        [Test]
        public void WhitelistRefusesUnlistedTransitions()
        {
            var sm = new StateMachine<S>(S.Menu)
                .Allow(S.Menu, S.Playing)
                .Allow(S.Playing, S.Paused, S.GameOver)
                .Allow(S.Paused, S.Playing, S.Menu);
            Assert.That(sm.TryGo(S.GameOver), Is.False);
            Assert.That(sm.Current, Is.EqualTo(S.Menu));
            Assert.That(sm.TryGo(S.Playing), Is.True);
            Assert.That(sm.TryGo(S.Menu), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => sm.Go(S.Menu));
        }

        [Test]
        public void TimeInStateResetsOnChange()
        {
            float ticked = 0;
            var sm = new StateMachine<S>(S.Playing).OnTick(S.Playing, dt => ticked += dt);
            sm.Tick(0.5f);
            sm.Tick(0.25f);
            Assert.That(sm.TimeInState, Is.EqualTo(0.75f).Within(1e-6));
            Assert.That(ticked, Is.EqualTo(0.75f).Within(1e-6));
            sm.Go(S.Paused);
            Assert.That(sm.TimeInState, Is.EqualTo(0f));
        }

        [Test]
        public void ReenteringCurrentStateIsANoOp()
        {
            int enters = 0;
            var sm = new StateMachine<S>(S.Menu).OnEnter(S.Menu, () => enters++);
            Assert.That(sm.TryGo(S.Menu), Is.True);
            Assert.That(enters, Is.EqualTo(0));
        }
    }
}

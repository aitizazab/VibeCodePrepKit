using JamKit.Combat;
using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Presenting.Tests
{
    public class PresenterTimeTests
    {
        [Test]
        public void SlowMotionCyclesOneHalfQuarterAndBack()
        {
            var t = new PresenterTime();
            Assert.That(t.SlowMotion, Is.EqualTo(1f));
            Assert.That(t.CycleSlowMotion(), Is.EqualTo(0.5f));
            Assert.That(t.CycleSlowMotion(), Is.EqualTo(0.25f));
            Assert.That(t.CycleSlowMotion(), Is.EqualTo(1f));
            t.SetSlowStep(9);
            Assert.That(t.SlowMotion, Is.EqualTo(0.25f), "clamped to the last step");
            t.SetSlowStep(-3);
            Assert.That(t.SlowMotion, Is.EqualTo(1f));
        }

        [Test]
        public void FreezeAndBackstageStopTimeAndResetUndoesEverything()
        {
            var t = new PresenterTime();
            t.SetSlowStep(1);
            Assert.That(t.Factor, Is.EqualTo(0.5f));
            t.Frozen = true;
            Assert.That(t.Factor, Is.EqualTo(0f));
            t.Frozen = false;
            t.Backstage = true;
            Assert.That(t.Factor, Is.EqualTo(0f));
            Assert.That(t.IsDefault, Is.False);
            t.Reset();
            Assert.That(t.Factor, Is.EqualTo(1f));
            Assert.That(t.IsDefault, Is.True);
        }

        [Test]
        public void PlayingStacksHitStopDebugSpeedAndSlowMotion()
        {
            Assert.That(GameTime.Scale(GameState.Playing, false, 1f, 1f, 1f), Is.EqualTo(1f));
            Assert.That(GameTime.Scale(GameState.Playing, false, 1f, 1f, 0.5f), Is.EqualTo(0.5f));
            Assert.That(GameTime.Scale(GameState.Playing, false, 0.2f, 1f, 0.5f), Is.EqualTo(0.1f).Within(1e-6f), "a hit stop inside slow motion is slower still");
            Assert.That(GameTime.Scale(GameState.Playing, false, 1f, 2f, 0.25f), Is.EqualTo(0.5f), "debug x2 with presenter x0.25");
            Assert.That(GameTime.Scale(GameState.Playing, false, 0f, 1f, 0.5f), Is.EqualTo(0f), "a full hit stop still freezes");
            Assert.That(GameTime.Scale(GameState.Playing, false, 1f, 1f, 0f), Is.EqualTo(0f), "freeze frame");
        }

        [Test]
        public void PauseDialogueAndEndScreensStayStoppedWhateverTheSlowMotion()
        {
            foreach (var s in new[] { GameState.Paused, GameState.Dialogue, GameState.GameOver, GameState.Victory })
                Assert.That(GameTime.Scale(s, false, 1f, 2f, 0.5f), Is.EqualTo(0f), s.ToString());
            foreach (var s in new[] { GameState.Title, GameState.Credits, GameState.Boot })
                Assert.That(GameTime.Scale(s, false, 0.1f, 0.25f, 0f), Is.EqualTo(1f), s + " runs at normal speed (menus aren't slowed or frozen)");
            Assert.That(GameTime.Scale(GameState.Playing, true, 1f, 1f, 1f), Is.EqualTo(0f), "nothing moves behind a scene load");
        }

        [Test]
        public void HitStopTimingIsRealTimeSoSlowMotionDoesNotStretchIt()
        {
            var hs = new HitStop();
            var t = new PresenterTime();
            t.SetSlowStep(2);
            hs.Request(0.1f, 0f);
            Assert.That(GameTime.Scale(GameState.Playing, false, hs.CurrentTimeScale, 1f, t.Factor), Is.EqualTo(0f));
            hs.Tick(0.11f); // real seconds
            Assert.That(GameTime.Scale(GameState.Playing, false, hs.CurrentTimeScale, 1f, t.Factor), Is.EqualTo(0.25f), "back to slow motion, not to 1");
        }

        [Test]
        public void PausingInsideSlowMotionAndResumingKeepsTheSlowMotion()
        {
            var t = new PresenterTime();
            t.SetSlowStep(1);
            Assert.That(GameTime.Scale(GameState.Paused, false, 1f, 1f, t.Factor), Is.EqualTo(0f));
            Assert.That(GameTime.Scale(GameState.Playing, false, 1f, 1f, t.Factor), Is.EqualTo(0.5f));
        }

        [Test]
        public void InvincibilityOnlyEverProtectsThePlayer()
        {
            Assert.That(Invincibility.Blocks(true, false, false), Is.False);
            Assert.That(Invincibility.Blocks(true, true, false), Is.True, "debug god mode");
            Assert.That(Invincibility.Blocks(true, false, true), Is.True, "presenter invincible");
            Assert.That(Invincibility.Blocks(false, true, true), Is.False, "enemies still take damage");
        }

        [Test]
        public void HoldChordFiresOnceAfterTheHoldAndAgainOnlyAfterARelease()
        {
            var c = new HoldChord { holdSeconds = 1f };
            Assert.That(c.Tick(true, 0.5f), Is.False);
            Assert.That(c.Progress01, Is.EqualTo(0.5f));
            Assert.That(c.Tick(true, 0.6f), Is.True);
            Assert.That(c.Tick(true, 2f), Is.False, "once per hold");
            Assert.That(c.Tick(false, 0.1f), Is.False);
            Assert.That(c.Held, Is.EqualTo(0f));
            Assert.That(c.Tick(true, 0.9f), Is.False, "a new hold starts from zero");
            Assert.That(c.Tick(true, 0.1f), Is.True);
        }
    }
}

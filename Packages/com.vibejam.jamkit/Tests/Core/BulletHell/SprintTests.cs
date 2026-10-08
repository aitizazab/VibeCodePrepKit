using JamKit.BulletHell;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class SprintTests
    {
        static SprintMeter Meter(float stamina = 1.5f, float delay = 0.4f, float refill = 2f, float budget = 8f)
        {
            var m = new SprintMeter();
            m.Configure(new SprintDef { staminaSeconds = stamina, regenDelay = delay, refillSeconds = refill, roomBudgetSeconds = budget, restartFraction = 0.25f });
            m.Fill();
            return m;
        }

        /// <summary>Seconds of sprint over <paramref name="seconds"/> of holding the button, at a fixed step.</summary>
        static float SprintSeconds(SprintMeter m, float seconds, float dt, bool held = true, bool moving = true)
        {
            float total = 0f;
            int steps = (int)System.Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) total += m.Tick(dt, held, moving) * dt;
            return total;
        }

        [Test]
        public void DefaultsMatchTheDesign()
        {
            var d = new SprintDef();
            Assert.That(d.speedMultiplier, Is.EqualTo(1.6f));
            Assert.That(d.staminaSeconds, Is.EqualTo(1.5f));
            Assert.That(d.regenDelay, Is.EqualTo(0.4f));
            Assert.That(d.refillSeconds, Is.EqualTo(2f));
            Assert.That(d.roomBudgetSeconds, Is.EqualTo(8f));
            var m = new SprintMeter();
            Assert.That(m.StaminaFraction, Is.EqualTo(1f), "starts full");
            Assert.That(m.BudgetFraction, Is.EqualTo(1f));
            Assert.That(m.CanSprint, Is.True);
        }

        [Test]
        public void SprintingDrainsStaminaAndTheRoomBudget()
        {
            var m = Meter();
            Assert.That(m.Tick(0.5f, true, true), Is.EqualTo(1f), "the whole frame was sprint");
            Assert.That(m.IsSprinting, Is.True);
            Assert.That(m.Stamina, Is.EqualTo(1f).Within(1e-4));
            Assert.That(m.RoomBudget, Is.EqualTo(7.5f).Within(1e-4));
            Assert.That(m.Tick(0.5f, false, true), Is.EqualTo(0f), "let go");
            Assert.That(m.IsSprinting, Is.False);
            Assert.That(m.Tick(0.5f, true, false), Is.EqualTo(0f), "standing still never spends stamina");
        }

        [Test]
        public void RunningDryStopsTheSprintPartWayThroughAFrame()
        {
            var m = Meter();
            Assert.That(m.Tick(1.2f, true, true), Is.EqualTo(1f));
            float k = m.Tick(0.6f, true, true);
            Assert.That(k, Is.EqualTo(0.5f).Within(1e-4), "only the 0.3 s left counts");
            Assert.That(m.StaminaEmptiedThisTick, Is.True, "flash and sound now");
            Assert.That(m.Exhausted, Is.True);
            Assert.That(m.CanSprint, Is.False, "no sprint at 0");
            Assert.That(m.Tick(0.1f, true, true), Is.EqualTo(0f));
            Assert.That(m.StaminaEmptiedThisTick, Is.False, "the empty event fires once");
        }

        [Test]
        public void StaminaComesBackAfterTheDelay()
        {
            var m = Meter();
            m.Tick(1.5f, true, true);
            Assert.That(m.Stamina, Is.EqualTo(0f));
            m.Tick(0.3f, false, true);
            Assert.That(m.Stamina, Is.EqualTo(0f), "still in the delay");
            m.Tick(0.2f, false, true); // 0.1 s past the delay
            Assert.That(m.Stamina, Is.EqualTo(0.1f * 1.5f / 2f).Within(1e-4), "refills the bar in 2 s");
            m.Tick(5f, false, true);
            Assert.That(m.StaminaFraction, Is.EqualTo(1f), "never above full");
        }

        [Test]
        public void AfterRunningDryItWaitsForAQuarterBar()
        {
            var m = Meter();
            m.Tick(1.5f, true, true);
            // Holding the button the whole time: 0.4 s delay, then 25% of 1.5 s at 0.75 per second = 0.5 s.
            float k = 0f;
            for (int i = 0; i < 8; i++) k += m.Tick(0.1f, true, true); // 0.8 s: not yet
            Assert.That(k, Is.EqualTo(0f));
            Assert.That(m.Exhausted, Is.True);
            m.Tick(0.15f, true, true);
            Assert.That(m.Exhausted, Is.False, "a quarter bar is back");
            Assert.That(m.Tick(0.05f, true, true), Is.GreaterThan(0f), "sprint resumes");
        }

        [Test]
        public void TheRoomBudgetCapsTotalSprintUntilTheNextRoom()
        {
            var m = Meter();
            // Sprint in bursts with pauses to recharge: the bar refills, the budget doesn't.
            float total = 0f;
            for (int burst = 0; burst < 20; burst++)
            {
                total += SprintSeconds(m, 1.5f, 0.05f);
                SprintSeconds(m, 3f, 0.05f, false);
            }
            Assert.That(total, Is.EqualTo(8f).Within(0.01f), "never more than the room's 8 s");
            Assert.That(m.BudgetEmpty, Is.True);
            Assert.That(m.CanSprint, Is.False);
            Assert.That(m.StaminaFraction, Is.EqualTo(1f), "the bar itself is full again");
            m.RefillRoom();
            Assert.That(m.BudgetFraction, Is.EqualTo(1f), "a new fight room refills the tank");
            Assert.That(m.Tick(0.1f, true, true), Is.EqualTo(1f));
        }

        [Test]
        public void TheBudgetEmptyEventFiresOnce()
        {
            var m = Meter(stamina: 5f, budget: 1f);
            Assert.That(m.Tick(0.8f, true, true), Is.EqualTo(1f));
            Assert.That(m.BudgetEmptiedThisTick, Is.False);
            Assert.That(m.Tick(0.4f, true, true), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(m.BudgetEmptiedThisTick, Is.True);
            m.Tick(0.4f, true, true);
            Assert.That(m.BudgetEmptiedThisTick, Is.False);
        }

        [Test]
        public void FrameRateIndependent()
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 0.1f })
            {
                var m = Meter();
                // Hold 1 s, let go 1 s, hold 3 s: the same sprint time at any frame rate.
                float total = SprintSeconds(m, 1f, dt) + SprintSeconds(m, 1f, dt, false) + SprintSeconds(m, 3f, dt);
                var reference = Meter();
                float expected = SprintSeconds(reference, 1f, 0.001f) + SprintSeconds(reference, 1f, 0.001f, false) + SprintSeconds(reference, 3f, 0.001f);
                Assert.That(total, Is.EqualTo(expected).Within(0.06f), "dt " + dt);
                Assert.That(m.RoomBudget, Is.EqualTo(reference.RoomBudget).Within(0.06f), "dt " + dt);
            }
        }

        [Test]
        public void UpgradesScaleTheMeterThroughStats()
        {
            var loadout = new ShmupLoadout();
            foreach (var key in new[] { ShmupStats.SprintSpeed, ShmupStats.StaminaMax, ShmupStats.StaminaRegen, ShmupStats.RoomSprintBudget })
            {
                Assert.That(System.Array.IndexOf(ShmupStats.All, key), Is.GreaterThanOrEqualTo(0), key + " is a known stat");
                Assert.That(loadout.Stats.Get(key), Is.EqualTo(1f), key + " base 1");
            }
            Assert.That(loadout.SprintMultiplier(1.6f), Is.EqualTo(1.6f).Within(1e-5));
            loadout.Stats.SetBase(ShmupStats.SprintSpeed, 1.5f);
            Assert.That(loadout.SprintMultiplier(1.6f), Is.EqualTo(1.9f).Within(1e-5), "the bonus grows, not the whole speed");

            var m = new SprintMeter();
            m.Configure(new SprintDef(), staminaScale: 2f, regenScale: 1f, budgetScale: 0.5f);
            m.Fill();
            Assert.That(m.StaminaMax, Is.EqualTo(3f).Within(1e-5));
            Assert.That(m.RoomBudgetMax, Is.EqualTo(4f).Within(1e-5));
            m.Configure(new SprintDef { staminaSeconds = float.NaN, roomBudgetSeconds = -3f });
            Assert.That(m.StaminaMax, Is.EqualTo(1.5f), "bad data falls back");
            Assert.That(m.RoomBudgetMax, Is.EqualTo(0f));
            m.Fill();
            Assert.That(m.CanSprint, Is.False, "a zero budget means no sprint");
        }

        [Test]
        public void BadTimeStepsDoNothing()
        {
            var m = Meter();
            Assert.That(m.Tick(float.NaN, true, true), Is.EqualTo(0f));
            Assert.That(m.Tick(-1f, true, true), Is.EqualTo(0f));
            Assert.That(m.Tick(0f, true, true), Is.EqualTo(0f));
            Assert.That(m.StaminaFraction, Is.EqualTo(1f));
        }
    }
}
